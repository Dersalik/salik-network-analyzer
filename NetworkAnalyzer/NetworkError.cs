using System.Net;

namespace NetworkAnalyzer
{
    /// <summary>
    /// Base class for all network-related errors using a discriminated union pattern.
    /// </summary>
    public abstract record NetworkError
    {
        /// <summary>
        /// Gets the human-readable error message.
        /// </summary>
        public abstract string Message { get; }

        /// <summary>
        /// Represents a ping operation failure to a specific target.
        /// </summary>
        /// <param name="Target">The IP address that failed to respond.</param>
        /// <param name="ErrorMessage">The detailed error message.</param>
        public sealed record PingFailed(IPAddress Target, string ErrorMessage) : NetworkError
        {
            public override string Message => $"Ping to {Target} failed: {ErrorMessage}";
        }

        /// <summary>
        /// Represents a batch ping operation failure.
        /// </summary>
        /// <param name="ErrorMessage">The detailed error message.</param>
        public sealed record BatchPingFailed(string ErrorMessage) : NetworkError
        {
            public override string Message => $"Batch ping operation failed: {ErrorMessage}";
        }

        /// <summary>
        /// Represents a trace route operation failure.
        /// </summary>
        /// <param name="Target">The destination IP address that failed.</param>
        /// <param name="ErrorMessage">The detailed error message.</param>
        public sealed record TraceRouteFailed(IPAddress Target, string ErrorMessage) : NetworkError
        {
            public override string Message => $"Trace route to {Target} failed: {ErrorMessage}";
        }

        /// <summary>
        /// Represents a network discovery operation failure.
        /// </summary>
        /// <param name="ErrorMessage">The detailed error message.</param>
        public sealed record NetworkDiscoveryFailed(string ErrorMessage) : NetworkError
        {
            public override string Message => $"Network discovery failed: {ErrorMessage}";
        }

        /// <summary>
        /// Represents a DNS resolution failure for a hostname.
        /// </summary>
        /// <param name="HostName">The hostname that failed to resolve.</param>
        /// <param name="ErrorMessage">The detailed error message.</param>
        public sealed record DnsResolutionFailed(string HostName, string ErrorMessage) : NetworkError
        {
            public override string Message => $"DNS resolution for {HostName} failed: {ErrorMessage}";
        }

        /// <summary>
        /// Represents a network interface error.
        /// </summary>
        /// <param name="ErrorMessage">The detailed error message.</param>
        public sealed record NetworkInterfaceError(string ErrorMessage) : NetworkError
        {
            public override string Message => $"Network interface error: {ErrorMessage}";
        }

        /// <summary>
        /// Represents a port scan operation failure.
        /// </summary>
        /// <param name="Target">The IP address that failed to scan.</param>
        /// <param name="ErrorMessage">The detailed error message.</param>
        public sealed record PortScanFailed(IPAddress Target, string ErrorMessage) : NetworkError
        {
            public override string Message => $"Port scan of {Target} failed: {ErrorMessage}";
        }

        /// <summary>
        /// Represents an invalid network range specification error.
        /// </summary>
        /// <param name="Range">The invalid network range string.</param>
        /// <param name="ErrorMessage">The detailed error message.</param>
        public sealed record InvalidNetworkRange(string Range, string ErrorMessage) : NetworkError
        {
            public override string Message => $"Invalid network range '{Range}': {ErrorMessage}";
        }
    }
}
