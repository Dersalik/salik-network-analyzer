using LanguageExt;
using System.Net;
using static LanguageExt.Prelude;

namespace NetworkAnalyzer;

/// <summary>
/// Shared utility functions for network operations.
/// </summary>
public static class NetworkUtilities
{
    /// <summary>
    /// Attempts to resolve the hostname for an IP address via reverse DNS lookup.
    /// </summary>
    /// <param name="ipAddress">The IP address to resolve.</param>
    /// <returns>The hostname if resolution succeeds, or None if it fails.</returns>
    public static async Task<Option<string>> ResolveHostNameAsync(IPAddress ipAddress)
    {
        return await TryAsync(async () =>
        {
            var hostEntry = await Dns.GetHostEntryAsync(ipAddress);
            return hostEntry.HostName;
        }).Match(
            Succ: Some,
            Fail: _ => None
        );
    }

    /// <summary>
    /// Infers the device type based on hostname patterns.
    /// </summary>
    /// <param name="device">The network device to analyze.</param>
    /// <returns>The inferred device type based on hostname patterns.</returns>
    public static NetworkDeviceType InferDeviceType(NetworkDevice device)
    {
        return device.HostName.Match(
            Some: hostName =>
            {
                var lowerName = hostName.ToLowerInvariant();
                if (lowerName.Contains("router") || lowerName.Contains("gateway"))
                    return NetworkDeviceType.Router;
                if (lowerName.Contains("switch"))
                    return NetworkDeviceType.Switch;
                if (lowerName.Contains("printer"))
                    return NetworkDeviceType.Printer;
                if (lowerName.Contains("server"))
                    return NetworkDeviceType.Server;
                if (lowerName.Contains("phone") || lowerName.Contains("mobile"))
                    return NetworkDeviceType.MobileDevice;

                return NetworkDeviceType.Computer;
            },
            None: () => NetworkDeviceType.Unknown
        );
    }
}
