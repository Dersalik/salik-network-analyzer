using LanguageExt;
using System.Net;
using System.Net.NetworkInformation;
using static LanguageExt.Prelude;

namespace NetworkAnalyzer;

/// <summary>
/// Configuration options for network analysis operations.
/// </summary>
/// <param name="PingConfig">Configuration for ping operations.</param>
/// <param name="MaxConcurrency">Maximum number of concurrent operations allowed.</param>
/// <param name="IncludeTraceRoute">Whether to include trace route analysis.</param>
/// <param name="IncludePortScan">Whether to include port scanning (currently not implemented).</param>
/// <param name="TargetNetwork">Optional target network in CIDR notation (e.g., "192.168.1.0/24").</param>
public readonly record struct AnalysisOptions(
    PingConfiguration PingConfig,
    int MaxConcurrency,
    bool IncludeTraceRoute,
    bool IncludePortScan,
    Option<string> TargetNetwork
)
{
    /// <summary>
    /// Gets the default analysis options with conservative settings.
    /// </summary>
    public static readonly AnalysisOptions Default = new(
        PingConfiguration.Default,
        50,
        false,
        true,
        None
    );
}

/// <summary>
/// Represents the complete results of a network analysis operation.
/// </summary>
/// <param name="DiscoveredDevices">Sequence of devices discovered during the analysis.</param>
/// <param name="NetworkInterfaces">Network interfaces on the local machine.</param>
/// <param name="TraceRouteResults">Optional trace route results if requested.</param>
/// <param name="AnalysisTimestamp">UTC timestamp when the analysis started.</param>
/// <param name="AnalysisDuration">Total time taken for the analysis.</param>
/// <param name="Statistics">Aggregated statistics from the analysis.</param>
public readonly record struct NetworkAnalysisResult(
    Seq<NetworkDevice> DiscoveredDevices,
    Seq<NetworkInterface> NetworkInterfaces,
    Option<Seq<IPAddress>> TraceRouteResults,
    DateTime AnalysisTimestamp,
    TimeSpan AnalysisDuration,
    AnalysisStatistics Statistics
);

/// <summary>
/// Statistical summary of a network analysis operation.
/// </summary>
/// <param name="TotalDevicesScanned">Total number of IP addresses scanned.</param>
/// <param name="ActiveDevices">Number of devices that responded to ping.</param>
/// <param name="InactiveDevices">Number of devices that did not respond.</param>
/// <param name="SuccessRate">Percentage of devices that responded (0-100).</param>
/// <param name="AverageResponseTime">Average response time in milliseconds.</param>
/// <param name="MinResponseTime">Minimum response time in milliseconds.</param>
/// <param name="MaxResponseTime">Maximum response time in milliseconds.</param>
public readonly record struct AnalysisStatistics(
    int TotalDevicesScanned,
    int ActiveDevices,
    int InactiveDevices,
    double SuccessRate,
    long AverageResponseTime,
    long MinResponseTime,
    long MaxResponseTime
)
{
    /// <summary>
    /// Creates statistics from a collection of discovered devices.
    /// </summary>
    /// <param name="devices">The devices to analyze.</param>
    /// <param name="totalScanned">Total number of IP addresses that were scanned.</param>
    /// <returns>Aggregated statistics for the device collection.</returns>
    public static AnalysisStatistics FromDevices(Seq<NetworkDevice> devices, int totalScanned)
    {
        var activeDevices = devices.Where(d => d.IsReachable).ToSeq();
        var responseTimes = activeDevices
            .Choose(d => d.ResponseTime)
            .ToArray();

        return new AnalysisStatistics(
            totalScanned,
            activeDevices.Count,
            totalScanned - activeDevices.Count,
            totalScanned > 0 ? (double)activeDevices.Count / totalScanned * 100 : 0,
            responseTimes.Length > 0 ? (long)responseTimes.Average() : 0,
            responseTimes.Length > 0 ? responseTimes.Min() : 0,
            responseTimes.Length > 0 ? responseTimes.Max() : 0
        );
    }
}

/// <summary>
/// Main engine for performing network analysis operations including discovery, ping, and trace route.
/// </summary>
public static class NetworkAnalyzerEngine
{
    /// <summary>
    /// Performs a comprehensive network analysis operation.
    /// </summary>
    /// <param name="options">Configuration options for the analysis. Uses defaults if not specified.</param>
    /// <returns>Either a network error or the complete analysis results.</returns>
    public static async Task<Either<NetworkError, NetworkAnalysisResult>> AnalyzeNetworkAsync(
        AnalysisOptions options = default)
    {
        var actualOptions = options.Equals(default) ? AnalysisOptions.Default : options;
        var startTime = DateTime.UtcNow;

        var result = await TryAsync(async () =>
        {
            var networkInterfaces = NetworkDiscovery.GetNetworkInterfaces();

            return await networkInterfaces.Match(
                Right: async interfaces =>
                {
                    var discoveredDevices = await actualOptions.TargetNetwork.Match(
                        Some: async network =>
                        {
                            var range = NetworkRange.FromCidr(network);
                            return await range.Match(
                                Right: async r => await NetworkDiscovery.DiscoverDevicesAsync(
                                    r, actualOptions.PingConfig, actualOptions.MaxConcurrency),
                                Left: error => Task.FromResult<Either<NetworkError, Seq<NetworkDevice>>>(Left(error))
                            );
                        },
                        None: async () => await NetworkDiscovery.DiscoverLocalNetworkAsync(actualOptions.PingConfig)
                    );

                    return await discoveredDevices.Match(
                        Right: async devices =>
                        {
                            var traceRouteResults = actualOptions.IncludeTraceRoute && devices.Any()
                                ? await PerformTraceRouteAnalysis(devices.Head.IpAddress)
                                : None;

                            var endTime = DateTime.UtcNow;
                            var totalScanned = actualOptions.TargetNetwork.Match(
                                Some: network =>
                                {
                                    var range = NetworkRange.FromCidr(network);
                                    return range.Match(
                                        Right: r => r.GetAddresses().Count(),
                                        Left: _ => devices.Count
                                    );
                                },
                                None: () => devices.Count + (256 - devices.Count)
                            );

                            var statistics = AnalysisStatistics.FromDevices(devices, totalScanned);

                            return new NetworkAnalysisResult(
                                devices,
                                interfaces,
                                traceRouteResults,
                                startTime,
                                endTime - startTime,
                                statistics
                            );
                        },
                        Left: error => throw new Exception(error.Message)
                    );
                },
                Left: error => throw new Exception(error.Message)
            );
        });

        return result.Match(
            Succ: analysisResult => Right<NetworkError, NetworkAnalysisResult>(analysisResult),
            Fail: ex => Left<NetworkError, NetworkAnalysisResult>(new NetworkError.NetworkDiscoveryFailed(ex.Message))
        );
    }

    /// <summary>
    /// Analyzes a single network device by IP address.
    /// </summary>
    /// <param name="target">The IP address of the device to analyze.</param>
    /// <param name="pingConfig">Optional ping configuration. Uses defaults if not specified.</param>
    /// <returns>Either a network error or the analyzed device information.</returns>
    public static async Task<Either<NetworkError, NetworkDevice>> AnalyzeSingleDeviceAsync(
        IPAddress target,
        PingConfiguration pingConfig = default)
    {
        var result = await TryAsync(async () =>
        {
            var pingResult = await PingService.PingAsync(target, pingConfig);

            return await pingResult.Match(
                Right: async result =>
                {
                    var device = NetworkDevice.Create(target)
                        .WithPingResult(result.success, result.ResponseTime, result.Status);

                    var hostName = await NetworkUtilities.ResolveHostNameAsync(target);
                    device = hostName.Match(
                        Some: name => device.WithHostName(name),
                        None: () => device
                    );

                    var deviceType = NetworkUtilities.InferDeviceType(device);
                    return device.WithDeviceType(deviceType);
                },
                Left: error => throw new Exception(error.Message)
            );
        });

        return result.Match(
            Succ: device => Right<NetworkError, NetworkDevice>(device),
            Fail: ex => Left<NetworkError, NetworkDevice>(new NetworkError.NetworkDiscoveryFailed(ex.Message))
        );
    }

    /// <summary>
    /// Performs continuous ping operations to a target device.
    /// </summary>
    /// <param name="target">The IP address to ping.</param>
    /// <param name="count">Number of ping attempts to perform. Defaults to 4.</param>
    /// <param name="interval">Time to wait between pings. Defaults to 1 second.</param>
    /// <param name="pingConfig">Optional ping configuration. Uses defaults if not specified.</param>
    /// <returns>Either a network error or a sequence of ping results.</returns>
    public static async Task<Either<NetworkError, Seq<PingResult>>> ContinuousPingAsync(
        IPAddress target,
        int count = 4,
        TimeSpan? interval = null,
        PingConfiguration pingConfig = default)
    {
        var actualInterval = interval ?? TimeSpan.FromSeconds(1);

        var result = await TryAsync(async () =>
        {
            var results = new List<PingResult>();

            for (int i = 0; i < count; i++)
            {
                var pingResult = await PingService.PingAsync(target, pingConfig);
                pingResult.IfRight(result => results.Add(result));

                if (i < count - 1)
                    await Task.Delay(actualInterval);
            }

            return results.ToSeq();
        });

        return result.Match(
            Succ: pingResults => Right<NetworkError, Seq<PingResult>>(pingResults),
            Fail: ex => Left<NetworkError, Seq<PingResult>>(new NetworkError.PingFailed(target, ex.Message))
        );
    }
    private static async Task<Option<Seq<IPAddress>>> PerformTraceRouteAnalysis(IPAddress target)
    {
        var traceResult = await PingService.TraceRouteAsync(target);
        return traceResult.Match(
            Right: Some,
            Left: _ => None
        );
    }
}
