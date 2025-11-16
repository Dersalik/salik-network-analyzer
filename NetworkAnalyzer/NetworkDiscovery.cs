using LanguageExt;
using System.Net;
using System.Net.NetworkInformation;
using static LanguageExt.Prelude;


namespace NetworkAnalyzer;

/// <summary>
/// Represents a range of IP addresses defined by start and end addresses.
/// </summary>
/// <param name="StartAddress">The starting IP address of the range.</param>
/// <param name="EndAddress">The ending IP address of the range.</param>
public readonly record struct NetworkRange(IPAddress StartAddress, IPAddress EndAddress)
{
    /// <summary>
    /// Creates a network range from CIDR notation (e.g., "192.168.1.0/24").
    /// </summary>
    /// <param name="cidr">The CIDR notation string.</param>
    /// <returns>Either a network error or the parsed network range.</returns>
    public static Either<NetworkError, NetworkRange> FromCidr(string cidr)
    {
        return Try(() =>
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2) throw new ArgumentException("Invalid CIDR format");

            var baseAddress = IPAddress.Parse(parts[0]);
            var prefixLength = int.Parse(parts[1]);

            if (prefixLength < 0 || prefixLength > 32)
                throw new ArgumentException("Invalid prefix length");

            var mask = ~((1u << 32 - prefixLength) - 1);
            var networkBytes = baseAddress.GetAddressBytes();
            var networkInt = BitConverter.ToUInt32(networkBytes.Reverse().ToArray(), 0);
            var startInt = networkInt & mask;
            var endInt = startInt | (1u << 32 - prefixLength) - 1;

            var startBytes = BitConverter.GetBytes(startInt).Reverse().ToArray();
            var endBytes = BitConverter.GetBytes(endInt).Reverse().ToArray();

            return new NetworkRange(new IPAddress(startBytes), new IPAddress(endBytes));
        }).Match(
            Succ: range => Right<NetworkError, NetworkRange>(range),
            Fail: error => Left<NetworkError, NetworkRange>(new NetworkError.InvalidNetworkRange(cidr, error.Message))
        );
    }

    /// <summary>
    /// Gets all IP addresses in this network range.
    /// </summary>
    /// <returns>An enumerable of IP addresses from start to end.</returns>
    public IEnumerable<IPAddress> GetAddresses()
    {
        var startBytes = StartAddress.GetAddressBytes();
        var endBytes = EndAddress.GetAddressBytes();
        var startInt = BitConverter.ToUInt32(startBytes.Reverse().ToArray(), 0);
        var endInt = BitConverter.ToUInt32(endBytes.Reverse().ToArray(), 0);

        for (uint i = startInt; i <= endInt; i++)
        {
            var bytes = BitConverter.GetBytes(i).Reverse().ToArray();
            yield return new IPAddress(bytes);
        }
    }
}

/// <summary>
/// Provides network discovery and scanning functionality for local and remote networks.
/// </summary>
public static class NetworkDiscovery
{
    /// <summary>
    /// Gets all active network interfaces on the local machine, excluding loopback.
    /// </summary>
    /// <returns>Either a network error or a sequence of active network interfaces.</returns>
    public static Either<NetworkError, Seq<NetworkInterface>> GetNetworkInterfaces()
    {
        var result = Try(() =>
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToSeq();
        });

        return result.Match(
            Succ: interfaces => Right<NetworkError, Seq<NetworkInterface>>(interfaces),
            Fail: ex => Left<NetworkError, Seq<NetworkInterface>>(new NetworkError.NetworkInterfaceError(ex.Message))
        );
    }

    /// <summary>
    /// Discovers all devices within a specified network range by pinging each address.
    /// </summary>
    /// <param name="range">The network range to scan.</param>
    /// <param name="pingConfig">Optional ping configuration. Uses defaults if not specified.</param>
    /// <param name="maxConcurrency">Maximum number of concurrent ping operations. Defaults to 500.</param>
    /// <returns>Either a network error or a sequence of discovered devices.</returns>
    public static async Task<Either<NetworkError, Seq<NetworkDevice>>> DiscoverDevicesAsync(
    NetworkRange range,
    PingConfiguration pingConfig = default,
    int maxConcurrency = 500)
    {
        var result = await TryAsync(async () =>
        {
            var addresses = range.GetAddresses().ToList();
            var pingResults = await PingService.PingMultipleAsync(addresses, pingConfig, maxConcurrency);

            return await pingResults.Match(
                Right: async results =>
                {
                    var devices = new List<NetworkDevice>();

                    foreach (var result in results.Where(r => r.success))
                    {
                        var device = NetworkDevice.Create(result.Target)
                            .WithPingResult(result.success, result.ResponseTime, result.Status);

                        var hostName = await NetworkUtilities.ResolveHostNameAsync(result.Target);
                        device = hostName.Match(
                            Some: name => device.WithHostName(name),
                            None: () => device
                        );

                        var deviceType = NetworkUtilities.InferDeviceType(device);
                        device = device.WithDeviceType(deviceType);

                        devices.Add(device);
                    }

                    return devices.ToSeq();
                },
                Left: error => throw new Exception(error.Message)
            );
        });

        return result.Match(
            Succ: devices => Right<NetworkError, Seq<NetworkDevice>>(devices),
            Fail: ex => Left<NetworkError, Seq<NetworkDevice>>(new NetworkError.NetworkDiscoveryFailed(ex.Message))
        );
    }

    /// <summary>
    /// Discovers all devices on the local network by scanning all active network interfaces.
    /// </summary>
    /// <param name="pingConfig">Optional ping configuration. Uses defaults if not specified.</param>
    /// <returns>Either a network error or a sequence of discovered devices.</returns>
    public static async Task<Either<NetworkError, Seq<NetworkDevice>>> DiscoverLocalNetworkAsync(
    PingConfiguration pingConfig = default)
    {
        var result = await TryAsync(async () =>
        {
            var interfaces = GetNetworkInterfaces();

            return await interfaces.Match(
                Right: async netInterfaces =>
                {
                    var allDevices = new List<NetworkDevice>();

                    foreach (var netInterface in netInterfaces)
                    {
                        var properties = netInterface.GetIPProperties();
                        var unicastAddresses = properties.UnicastAddresses
                            .Where(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork); // Added missing using

                        foreach (var addr in unicastAddresses)
                        {
                            var subnet = GetSubnetFromUnicastAddress(addr);
                            await subnet.Match(
                                Right: async networkRange =>
                                {
                                    var devices = await DiscoverDevicesAsync(networkRange, pingConfig);
                                    devices.IfRight(devs => allDevices.AddRange(devs));
                                },
                                Left: _ => Task.CompletedTask
                            );
                        }
                    }

                    return allDevices.Distinct().ToSeq();
                },
                Left: error => throw new Exception(error.Message)
            );
        });

        return result.Match(
            Succ: devices => Right<NetworkError, Seq<NetworkDevice>>(devices),
            Fail: ex => Left<NetworkError, Seq<NetworkDevice>>(new NetworkError.NetworkDiscoveryFailed(ex.Message))
        );
    }

    private static Either<NetworkError, NetworkRange> GetSubnetFromUnicastAddress(
    UnicastIPAddressInformation addressInfo)
    {
        var result = Try(() =>
        {
            var address = addressInfo.Address;
            var mask = addressInfo.IPv4Mask;

            if (mask == null)
                throw new ArgumentException("No subnet mask available");

            var addressBytes = address.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();

            var networkBytes = new byte[4];
            var broadcastBytes = new byte[4];

            for (int i = 0; i < 4; i++)
            {
                networkBytes[i] = (byte)(addressBytes[i] & maskBytes[i]);
                broadcastBytes[i] = (byte)(addressBytes[i] | ~maskBytes[i]);
            }

            return new NetworkRange(new IPAddress(networkBytes), new IPAddress(broadcastBytes));
        });

        return result.Match(
            Succ: range => Right<NetworkError, NetworkRange>(range),
            Fail: ex => Left<NetworkError, NetworkRange>(new NetworkError.InvalidNetworkRange(
                addressInfo.Address.ToString(), ex.Message))
        );
    }
}
