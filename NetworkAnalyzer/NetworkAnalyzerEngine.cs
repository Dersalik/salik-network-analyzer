using LanguageExt;
using System.Net;
using System.Net.NetworkInformation;
using static LanguageExt.Prelude;

namespace NetworkAnalyzer;

public readonly record struct AnalysisOptions(
    PingConfiguration PingConfig,
    int MaxConcurrency,
    bool IncludeTraceRoute,
    bool IncludePortScan,
    Option<string> TargetNetwork
)
{
    public static readonly AnalysisOptions Default = new(
        PingConfiguration.Default,
        50,
        false,
        true,
        None
    );
}

public readonly record struct NetworkAnalysisResult(
    Seq<NetworkDevice> DiscoveredDevices,
    Seq<NetworkInterface> NetworkInterfaces,
    Option<Seq<IPAddress>> TraceRouteResults,
    DateTime AnalysisTimestamp,
    TimeSpan AnalysisDuration,
    AnalysisStatistics Statistics
);

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

public static class NetworkAnalyzerEngine
{
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

                    var hostName = await ResolveHostNameAsync(target);
                    device = hostName.Match(
                        Some: name => device.WithHostName(name),
                        None: () => device
                    );

                    var deviceType = InferDeviceType(device);
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

    private static async Task<Option<string>> ResolveHostNameAsync(IPAddress ipAddress)
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

    private static NetworkDeviceType InferDeviceType(NetworkDevice device)
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
