using LanguageExt;
using System.Net;
using System.Net.NetworkInformation;
using static LanguageExt.Prelude;
namespace NetworkAnalyzer;

public readonly record struct PingConfiguration(
    int Timeout,
    int BufferSize,
    int Ttl,
    bool DontFragment
)
{
    public static readonly PingConfiguration Default = new(5000, 32, 64, true);
}

public readonly record struct PingResult(
    IPAddress Target,
    bool success,
    long ResponseTime,
    IPStatus Status,
    DateTime Timestamp
    )
{
    public static PingResult Failed(IPAddress target, IPStatus status) =>
        new(target, false, 0, status, DateTime.UtcNow);

    public static PingResult Success(IPAddress target, long responseTime) =>
        new(target, true, responseTime, IPStatus.Success, DateTime.UtcNow);
}

public static class PingService
{
    public static async Task<Either<NetworkError, PingResult>> PingAsync(
        IPAddress target,
        PingConfiguration config)
    {
        var actualConfig = config.Equals(default) ? PingConfiguration.Default : config;

        var result = await TryAsync(async () =>
        {
            using var ping = new Ping();
            var options = new PingOptions(actualConfig.Ttl, actualConfig.DontFragment);
            var buffer = new byte[actualConfig.BufferSize];

            var reply = await ping.SendPingAsync(target, actualConfig.Timeout, buffer, options);

            return reply.Status == IPStatus.Success
                ? PingResult.Success(target, reply.RoundtripTime)
                : PingResult.Failed(target, reply.Status);
        });

        return result.Match(
            Succ: pingResult => Right<NetworkError, PingResult>(pingResult),
            Fail: error => Left<NetworkError, PingResult>(new NetworkError.PingFailed(target, error.Message))
        );
    }

    public static async Task<Either<NetworkError, Seq<PingResult>>> PingMultipleAsync(
        IEnumerable<IPAddress> targets,
        PingConfiguration config = default,
        int maxConcurrency = 10)
    {
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var result = await TryAsync(async () =>
        {
            var tasks = targets.Select(async target =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var result = await PingAsync(target, config);
                    return result.Match(
                        Right: r => r,
                        Left: _ => PingResult.Failed(target, IPStatus.TimedOut)
                    );
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            return results.ToSeq();
        });

        return result.Match(
            Succ: pingResults => Right<NetworkError, Seq<PingResult>>(pingResults),
            Fail: error => Left<NetworkError, Seq<PingResult>>(new NetworkError.BatchPingFailed(error.Message))
        );
    }

    public static async Task<Either<NetworkError, Seq<IPAddress>>> TraceRouteAsync(
        IPAddress target,
        int maxHops = 30,
        int timeout = 5000)
    {
        var result = await TryAsync(async () =>
        {
            var hops = new List<IPAddress>();
            for (int ttl = 1; ttl <= maxHops; ttl++)
            {
                using var ping = new Ping();
                var options = new PingOptions(ttl, true);
                var buffer = new byte[32];

                var reply = await ping.SendPingAsync(target, timeout, buffer, options);

                if (reply.Status == IPStatus.Success)
                {
                    hops.Add(reply.Address);
                    break;
                }
                else if (reply.Status == IPStatus.TtlExpired && reply.Address != null)
                {
                    hops.Add(reply.Address);
                }
                else if (reply.Status == IPStatus.TimedOut)
                {
                    continue;
                }
                else
                {
                    break;
                }
            }
            return hops.ToSeq();
        });
        return result.Match(
            Succ: hops => Right<NetworkError, Seq<IPAddress>>(hops),
            Fail: error => Left<NetworkError, Seq<IPAddress>>(new NetworkError.TraceRouteFailed(target, error.Message))
        );
    }
}
