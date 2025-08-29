using System.Net;

namespace NetworkAnalyzer
{
    public abstract record NetworkError
    {
        public abstract string Message { get; }

        public sealed record PingFailed(IPAddress Target, string ErrorMessage) : NetworkError
        {
            public override string Message => $"Ping to {Target} failed: {ErrorMessage}";
        }

        public sealed record BatchPingFailed(string ErrorMessage) : NetworkError
        {
            public override string Message => $"Batch ping operation failed: {ErrorMessage}";
        }

        public sealed record TraceRouteFailed(IPAddress Target, string ErrorMessage) : NetworkError
        {
            public override string Message => $"Trace route to {Target} failed: {ErrorMessage}";
        }

        public sealed record NetworkDiscoveryFailed(string ErrorMessage) : NetworkError
        {
            public override string Message => $"Network discovery failed: {ErrorMessage}";
        }

        public sealed record DnsResolutionFailed(string HostName, string ErrorMessage) : NetworkError
        {
            public override string Message => $"DNS resolution for {HostName} failed: {ErrorMessage}";
        }

        public sealed record NetworkInterfaceError(string ErrorMessage) : NetworkError
        {
            public override string Message => $"Network interface error: {ErrorMessage}";
        }

        public sealed record PortScanFailed(IPAddress Target, string ErrorMessage) : NetworkError
        {
            public override string Message => $"Port scan of {Target} failed: {ErrorMessage}";
        }

        public sealed record InvalidNetworkRange(string Range, string ErrorMessage) : NetworkError
        {
            public override string Message => $"Invalid network range '{Range}': {ErrorMessage}";
        }
    }
}
