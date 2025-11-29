using LanguageExt;
using System.Net;
using System.Net.NetworkInformation;
using static LanguageExt.Prelude;
namespace NetworkAnalyzer;

/// <summary>
/// Configuration settings for ping operations.
/// </summary>
/// <param name="Timeout">Timeout in milliseconds for ping responses.</param>
/// <param name="BufferSize">Size of the ping buffer in bytes.</param>
/// <param name="Ttl">Time-to-live value for ping packets.</param>
/// <param name="DontFragment">Whether to prevent packet fragmentation.</param>
public readonly record struct PingConfiguration(
    int Timeout,
    int BufferSize,
    int Ttl,
    bool DontFragment
)
{
    /// <summary>
    /// Gets the default ping configuration (5000ms timeout, 32-byte buffer, TTL 64, fragmentation disabled).
    /// </summary>
    public static readonly PingConfiguration Default = new(5000, 32, 64, true);
}

/// <summary>
/// Represents the result of a ping operation to a network device.
/// </summary>
/// <param name="Target">The IP address that was pinged.</param>
/// <param name="success">Whether the ping was successful.</param>
/// <param name="ResponseTime">Round-trip time in milliseconds.</param>
/// <param name="Status">The status code from the ping operation.</param>
/// <param name="Timestamp">UTC timestamp when the ping was performed.</param>
public readonly record struct PingResult(
    IPAddress Target,
    bool success,
    long ResponseTime,
    IPStatus Status,
    DateTime Timestamp
    )
{
    /// <summary>
    /// Creates a failed ping result.
    /// </summary>
    /// <param name="target">The IP address that was pinged.</param>
    /// <param name="status">The failure status code.</param>
    /// <returns>A ping result indicating failure.</returns>
    public static PingResult Failed(IPAddress target, IPStatus status) =>
        new(target, false, 0, status, DateTime.UtcNow);

    /// <summary>
    /// Creates a successful ping result.
    /// </summary>
    /// <param name="target">The IP address that was pinged.</param>
    /// <param name="responseTime">The round-trip time in milliseconds.</param>
    /// <returns>A ping result indicating success.</returns>
    public static PingResult Success(IPAddress target, long responseTime) =>
        new(target, true, responseTime, IPStatus.Success, DateTime.UtcNow);
}

/// <summary>
/// Provides low-level network ping operations including single ping, batch ping, and trace route.
/// </summary>
public static class PingService
{
    /// <summary>
    /// Pings a single IP address and returns the result.
    /// </summary>
    /// <param name="target">The IP address to ping.</param>
    /// <param name="config">Optional ping configuration. Uses defaults if not specified.</param>
    /// <returns>Either a network error or the ping result.</returns>
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

    /// <summary>
    /// Pings multiple IP addresses concurrently with controlled concurrency.
    /// </summary>
    /// <param name="targets">The collection of IP addresses to ping.</param>
    /// <param name="config">Optional ping configuration. Uses defaults if not specified.</param>
    /// <param name="maxConcurrency">Maximum number of concurrent ping operations. Defaults to 10.</param>
    /// <returns>Either a network error or a sequence of ping results.</returns>
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

    /// <summary>
    /// Performs a trace route to discover the network path to a target IP address.
    /// </summary>
    /// <param name="target">The destination IP address to trace.</param>
    /// <param name="maxHops">Maximum number of hops to trace. Defaults to 30.</param>
    /// <param name="timeout">Timeout in milliseconds for each hop. Defaults to 5000.</param>
    /// <returns>Either a network error or a sequence of IP addresses representing the route.</returns>
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
