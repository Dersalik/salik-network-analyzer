using NetworkAnalyzer;
using System.Net;
using Xunit;

namespace Tests
{
    public class NetworkErrorTests
    {
        [Fact]
        public void PingFailed_ShouldGenerateCorrectMessage()
        {
            // Arrange
            var target = IPAddress.Parse("192.168.1.1");
            var errorMessage = "Network unreachable";

            // Act 
            var error = new NetworkError.PingFailed(target, errorMessage);

            // Assert
            Assert.Equal("Ping to 192.168.1.1 failed: Network unreachable", error.Message);
        }

        [Fact]
        public void BatchPingFailed_ShouldGenerateCorrectMessage()
        {
            // Arrange
            var errorMessage = "Connection timeout";

            // Act
            var error = new NetworkError.BatchPingFailed(errorMessage);

            // Assert
            Assert.Equal("Batch ping operation failed: Connection timeout", error.Message);
        }

        [Fact]
        public void TraceRouteFailed_ShouldGenerateCorrectMessage()
        {
            // Arrange
            var target = IPAddress.Parse("8.8.8.8");
            var errorMessage = "No route to host";

            // Act
            var error = new NetworkError.TraceRouteFailed(target, errorMessage);

            // Assert
            Assert.Equal("Trace route to 8.8.8.8 failed: No route to host", error.Message);
        }

        [Fact]
        public void NetworkDiscoveryFailed_ShouldGenerateCorrectMessage()
        {
            // Arrange
            var errorMessage = "Interface not available";

            // Act
            var error = new NetworkError.NetworkDiscoveryFailed(errorMessage);

            // Assert
            Assert.Equal("Network discovery failed: Interface not available", error.Message);
        }

        [Fact]
        public void DnsResolutionFailed_ShouldGenerateCorrectMessage()
        {
            // Arrange
            var hostName = "example.com";
            var errorMessage = "Name resolution failed";

            // Act
            var error = new NetworkError.DnsResolutionFailed(hostName, errorMessage);

            // Assert
            Assert.Equal("DNS resolution for example.com failed: Name resolution failed", error.Message);
        }

        [Fact]
        public void NetworkInterfaceError_ShouldGenerateCorrectMessage()
        {
            // Arrange
            var errorMessage = "Interface disabled";

            // Act
            var error = new NetworkError.NetworkInterfaceError(errorMessage);

            // Assert
            Assert.Equal("Network interface error: Interface disabled", error.Message);
        }

        [Fact]
        public void PortScanFailed_ShouldGenerateCorrectMessage()
        {
            // Arrange
            var target = IPAddress.Parse("10.0.0.1");
            var errorMessage = "Connection refused";

            // Act
            var error = new NetworkError.PortScanFailed(target, errorMessage);

            // Assert
            Assert.Equal("Port scan of 10.0.0.1 failed: Connection refused", error.Message);
        }

        [Fact]
        public void InvalidNetworkRange_ShouldGenerateCorrectMessage()
        {
            // Arrange
            var range = "192.168.1.0/33";
            var errorMessage = "Invalid prefix length";

            // Act
            var error = new NetworkError.InvalidNetworkRange(range, errorMessage);

            // Assert
            Assert.Equal("Invalid network range '192.168.1.0/33': Invalid prefix length", error.Message);
        }

        [Theory]
        [InlineData("127.0.0.1", "Timeout", "Ping to 127.0.0.1 failed: Timeout")]
        [InlineData("255.255.255.255", "Broadcast address", "Ping to 255.255.255.255 failed: Broadcast address")]
        [InlineData("0.0.0.0", "Invalid address", "Ping to 0.0.0.0 failed: Invalid address")]
        public void PingFailed_WithVariousIPAddresses_ShouldGenerateCorrectMessage(string targetIp, string errorMessage, string expectedMessage)
        {
            // Arrange
            var target = IPAddress.Parse(targetIp);

            // Act
            var error = new NetworkError.PingFailed(target, errorMessage);

            // Assert
            Assert.Equal(expectedMessage, error.Message);
        }

        [Theory]
        [InlineData("localhost", "Host not found", "DNS resolution for localhost failed: Host not found")]
        [InlineData("invalid-domain.invalid", "NXDOMAIN", "DNS resolution for invalid-domain.invalid failed: NXDOMAIN")]
        [InlineData("", "Empty hostname", "DNS resolution for  failed: Empty hostname")]
        public void DnsResolutionFailed_WithVariousHostnames_ShouldGenerateCorrectMessage(string hostName, string errorMessage, string expectedMessage)
        {
            // Act
            var error = new NetworkError.DnsResolutionFailed(hostName, errorMessage);

            // Assert
            Assert.Equal(expectedMessage, error.Message);
        }

        [Theory]
        [InlineData("192.168.1.0/24", "Valid range", "Invalid network range '192.168.1.0/24': Valid range")]
        [InlineData("10.0.0.0/8", "Large network", "Invalid network range '10.0.0.0/8': Large network")]
        [InlineData("invalid-range", "Parse error", "Invalid network range 'invalid-range': Parse error")]
        public void InvalidNetworkRange_WithVariousRanges_ShouldGenerateCorrectMessage(string range, string errorMessage, string expectedMessage)
        {
            // Act
            var error = new NetworkError.InvalidNetworkRange(range, errorMessage);

            // Assert
            Assert.Equal(expectedMessage, error.Message);
        }

        [Fact]
        public void NetworkError_IsAbstractRecord()
        {
            // Arrange & Act
            var type = typeof(NetworkError);

            // Assert
            Assert.True(type.IsAbstract);
            Assert.True(type.IsClass);
        }

        [Fact]
        public void NetworkError_SubTypesAreSealed()
        {
            // Arrange
            var types = new[]
            {
            typeof(NetworkError.PingFailed),
            typeof(NetworkError.BatchPingFailed),
            typeof(NetworkError.TraceRouteFailed),
            typeof(NetworkError.NetworkDiscoveryFailed),
            typeof(NetworkError.DnsResolutionFailed),
            typeof(NetworkError.NetworkInterfaceError),
            typeof(NetworkError.PortScanFailed),
            typeof(NetworkError.InvalidNetworkRange)
            };

            // Act & Assert
            foreach (var type in types)
            {
                Assert.True(type.IsSealed, $"{type.Name} should be sealed");
                Assert.True(typeof(NetworkError).IsAssignableFrom(type), $"{type.Name} should inherit from NetworkError");
            }
        }

        [Fact]
        public void NetworkError_RecordsHaveValueEquality()
        {
            // Arrange
            var target = IPAddress.Parse("192.168.1.1");
            var errorMessage = "Test error";

            // Act
            var error1 = new NetworkError.PingFailed(target, errorMessage);
            var error2 = new NetworkError.PingFailed(target, errorMessage);
            var error3 = new NetworkError.PingFailed(target, "Different error");

            // Assert
            Assert.Equal(error1, error2);
            Assert.NotEqual(error1, error3);
            Assert.Equal(error1.GetHashCode(), error2.GetHashCode());
        }

        [Fact]
        public void NetworkError_RecordsHaveDeconstructors()
        {
            // Arrange
            var target = IPAddress.Parse("10.0.0.1");
            var errorMessage = "Test deconstruction";

            // Act
            var error = new NetworkError.PingFailed(target, errorMessage);
            var (deconstructedTarget, deconstructedErrorMessage) = error;

            // Assert
            Assert.Equal(target, deconstructedTarget);
            Assert.Equal(errorMessage, deconstructedErrorMessage);
        }

        [Fact]
        public void NetworkError_ToStringReturnsCorrectFormat()
        {
            // Arrange
            var target = IPAddress.Parse("192.168.1.100");
            var errorMessage = "Connection timeout";

            // Act
            var error = new NetworkError.PingFailed(target, errorMessage);
            var result = error.ToString();

            // Assert
            Assert.Contains(target.ToString(), result);
            Assert.Contains(errorMessage, result);
        }

        [Fact]
        public void NetworkError_WithSpecialCharacters_ShouldHandleCorrectly()
        {
            // Arrange
            var errorMessage = "Error with special chars: @#$%^&*()";
            var hostName = "test-host.domain.com";

            // Act
            var error = new NetworkError.DnsResolutionFailed(hostName, errorMessage);

            // Assert
            Assert.Equal($"DNS resolution for {hostName} failed: {errorMessage}", error.Message);
        }

        [Fact]
        public void NetworkError_WithEmptyStrings_ShouldHandleCorrectly()
        {
            // Arrange
            var emptyErrorMessage = "";
            var emptyHostName = "";

            // Act
            var error = new NetworkError.DnsResolutionFailed(emptyHostName, emptyErrorMessage);

            // Assert
            Assert.Equal("DNS resolution for  failed: ", error.Message);
        }

        [Fact]
        public void NetworkError_WithIPv6Address_ShouldHandleCorrectly()
        {
            // Arrange
            var ipv6Address = IPAddress.Parse("2001:db8::1");
            var errorMessage = "IPv6 not supported";

            // Act
            var error = new NetworkError.PingFailed(ipv6Address, errorMessage);

            // Assert
            Assert.Equal("Ping to 2001:db8::1 failed: IPv6 not supported", error.Message);
        }

        [Fact]
        public void NetworkError_WithLongErrorMessage_ShouldHandleCorrectly()
        {
            // Arrange
            var longErrorMessage = new string('A', 1000);
            var target = IPAddress.Parse("172.16.0.1");

            // Act
            var error = new NetworkError.PingFailed(target, longErrorMessage);

            // Assert
            Assert.StartsWith("Ping to 172.16.0.1 failed:", error.Message);
            Assert.Contains(longErrorMessage, error.Message);
        }

        [Fact]
        public void NetworkError_PatternMatching_WorksCorrectly()
        {
            // Arrange
            var target = IPAddress.Parse("192.168.1.1");
            var errorMessage = "Test pattern matching";
            NetworkError error = new NetworkError.PingFailed(target, errorMessage);

            // Act & Assert
            var result = error switch
            {
                NetworkError.PingFailed pingError => $"Ping failed: {pingError.ErrorMessage}",
                NetworkError.DnsResolutionFailed dnsError => $"DNS failed: {dnsError.ErrorMessage}",
                _ => "Unknown error"
            };

            Assert.Equal("Ping failed: Test pattern matching", result);
        }

        [Fact]
        public void NetworkError_CopyWith_WorksCorrectly()
        {
            // Arrange
            var target = IPAddress.Parse("192.168.1.1");
            var originalError = new NetworkError.PingFailed(target, "Original message");

            // Act
            var modifiedError = originalError with { ErrorMessage = "Modified message" };

            // Assert
            Assert.Equal(target, modifiedError.Target);
            Assert.Equal("Modified message", modifiedError.ErrorMessage);
            Assert.Equal("Ping to 192.168.1.1 failed: Modified message", modifiedError.Message);
            Assert.NotEqual(originalError, modifiedError);
        }
    }
}
