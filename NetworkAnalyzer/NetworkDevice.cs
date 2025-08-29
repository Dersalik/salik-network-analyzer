using LanguageExt;
using System.Net;
using System.Net.NetworkInformation;
using static LanguageExt.Prelude;

namespace NetworkAnalyzer
{
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
        public static NetworkDevice Create(IPAddress ipAddress) =>
            new(ipAddress, None, None, false, None, None, NetworkDeviceType.Unknown);

        public NetworkDevice WithHostName(string hostName) =>
            this with { HostName = Some(hostName) };

        public NetworkDevice WithMacAddress(PhysicalAddress macAddress) =>
            this with { MacAddress = Some(macAddress) };

        public NetworkDevice WithPingResult(bool isReachable, long responseTime, IPStatus pingStatus) =>
            this with
            {
                IsReachable = isReachable,
                ResponseTime = Some(responseTime),
                PingStatus = Some(pingStatus)
            };

        public NetworkDevice WithDeviceType(NetworkDeviceType deviceType) =>
            this with { DeviceType = deviceType };

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
