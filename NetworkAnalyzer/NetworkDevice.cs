using LanguageExt;
using System.Net;
using System.Net.NetworkInformation;
using static LanguageExt.Prelude;

namespace NetworkAnalyzer
{
    /// <summary>
    /// Represents a network device discovered during network analysis.
    /// </summary>
    /// <param name="IpAddress">The IP address of the device.</param>
    /// <param name="HostName">Optional hostname resolved via DNS.</param>
    /// <param name="MacAddress">Optional MAC address of the device.</param>
    /// <param name="IsReachable">Whether the device responded to ping.</param>
    /// <param name="ResponseTime">Optional ping response time in milliseconds.</param>
    /// <param name="PingStatus">Optional ping status result.</param>
    /// <param name="DeviceType">The inferred type of the device.</param>
    public readonly record struct NetworkDevice(
        IPAddress IpAddress,
        Option<string> HostName,
        Option<PhysicalAddress> MacAddress,
        bool IsReachable,
        Option<long> ResponseTime,
        Option<IPStatus> PingStatus,
        NetworkDeviceType DeviceType
        )
    {
        /// <summary>
        /// Creates a new network device with only an IP address (builder pattern entry point).
        /// </summary>
        /// <param name="ipAddress">The IP address of the device.</param>
        /// <returns>A new network device with default values.</returns>
        public static NetworkDevice Create(IPAddress ipAddress) =>
            new(ipAddress, None, None, false, None, None, NetworkDeviceType.Unknown);

        /// <summary>
        /// Creates a copy of this device with the specified hostname.
        /// </summary>
        /// <param name="hostName">The hostname to set.</param>
        /// <returns>A new device instance with the hostname set.</returns>
        public NetworkDevice WithHostName(string hostName) =>
            this with { HostName = Some(hostName) };

        /// <summary>
        /// Creates a copy of this device with the specified MAC address.
        /// </summary>
        /// <param name="macAddress">The MAC address to set.</param>
        /// <returns>A new device instance with the MAC address set.</returns>
        public NetworkDevice WithMacAddress(PhysicalAddress macAddress) =>
            this with { MacAddress = Some(macAddress) };

        /// <summary>
        /// Creates a copy of this device with ping result information.
        /// </summary>
        /// <param name="isReachable">Whether the device is reachable.</param>
        /// <param name="responseTime">The ping response time in milliseconds.</param>
        /// <param name="pingStatus">The ping status result.</param>
        /// <returns>A new device instance with ping results set.</returns>
        public NetworkDevice WithPingResult(bool isReachable, long responseTime, IPStatus pingStatus) =>
            this with
            {
                IsReachable = isReachable,
                ResponseTime = Some(responseTime),
                PingStatus = Some(pingStatus)
            };

        /// <summary>
        /// Creates a copy of this device with the specified device type.
        /// </summary>
        /// <param name="deviceType">The device type to set.</param>
        /// <returns>A new device instance with the device type set.</returns>
        public NetworkDevice WithDeviceType(NetworkDeviceType deviceType) =>
            this with { DeviceType = deviceType };

        /// <summary>
        /// Gets a human-readable display name for this device.
        /// </summary>
        /// <returns>The hostname and IP if available, otherwise just the IP.</returns>
        public string GetDisplayName()
        {
            var ipAddress = IpAddress;
            return HostName.Match(
                Some: name => $"{name} ({ipAddress})",
                None: () => ipAddress.ToString()
            );
        }

        public override string ToString() => GetDisplayName();
    }

    /// <summary>
    /// Types of network devices that can be inferred from hostname patterns.
    /// </summary>
    public enum NetworkDeviceType
    {
        Unknown,
        Router,
        Switch,
        Computer,
        Printer,
        MobileDevice,
        IoTDevice,
        Server
    }
}
