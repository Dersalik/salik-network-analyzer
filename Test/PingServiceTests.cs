using NetworkAnalyzer;
using System.Net;
using System.Net.NetworkInformation;
using Xunit;

namespace Tests
{
    public class PingServiceTests
    {
        #region PingConfiguration Tests
        [Fact]
        public void PingConfiguration_DefaultValues_ShouldBeCorrect()
        {
            // Act
            var config = PingConfiguration.Default;

            // Assert
            Assert.Equal(5000, config.Timeout);
            Assert.Equal(32, config.BufferSize);
            Assert.Equal(64, config.Ttl);
            Assert.True(config.DontFragment);
        }

        [Fact]
        public void PingConfiguration_CustomValues_ShouldBeSetCorrectly()
        {
            // Act
            var config = new PingConfiguration(3000, 64, 128, false);

            // Assert
            Assert.Equal(3000, config.Timeout);
            Assert.Equal(64, config.BufferSize);
            Assert.Equal(128, config.Ttl);
            Assert.False(config.DontFragment);
        }

        [Fact]
        public void PingConfiguration_RecordEquality_ShouldWork()
        {
            // Arrange
            var config1 = new PingConfiguration(1000, 32, 64, true);
            var config2 = new PingConfiguration(1000, 32, 64, true);
            var config3 = new PingConfiguration(2000, 32, 64, true);

            // Assert
            Assert.Equal(config1, config2);
            Assert.NotEqual(config1, config3);
            Assert.Equal(config1.GetHashCode(), config2.GetHashCode());
        }

        [Fact]
        public void PingConfiguration_WithExpression_ShouldWork()
        {
            // Arrange
            var original = PingConfiguration.Default;

            // Act
            var modified = original with { Timeout = 10000 };

            // Assert
            Assert.Equal(10000, modified.Timeout);
            Assert.Equal(original.BufferSize, modified.BufferSize);
            Assert.Equal(original.Ttl, modified.Ttl);
            Assert.Equal(original.DontFragment, modified.DontFragment);
        }
        #endregion

        #region PingResult Tests

        [Fact]
        public void PingResult_Success_ShouldCreateCorrectResult()
        {
            // Arrange
            var target = IPAddress.Parse("127.0.0.1");
            var responseTime = 42L;

            // Act
            var result = PingResult.Success(target, responseTime);

            // Assert
            Assert.Equal(target, result.Target);
            Assert.True(result.success);
            Assert.Equal(responseTime, result.ResponseTime);
            Assert.Equal(IPStatus.Success, result.Status);
            Assert.True((DateTime.UtcNow - result.Timestamp).TotalSeconds < 1);
        }

        [Fact]
        public void PingResult_Failed_ShouldCreateCorrectResult()
        {
            // Arrange
            var target = IPAddress.Parse("192.168.1.1");
            var status = IPStatus.TimedOut;

            // Act
            var result = PingResult.Failed(target, status);

            // Assert
            Assert.Equal(target, result.Target);
            Assert.False(result.success);
            Assert.Equal(0, result.ResponseTime);
            Assert.Equal(status, result.Status);
            Assert.True((DateTime.UtcNow - result.Timestamp).TotalSeconds < 1);
        }

        [Theory]
        [InlineData(IPStatus.TimedOut)]
        [InlineData(IPStatus.DestinationHostUnreachable)]
        [InlineData(IPStatus.DestinationNetworkUnreachable)]
        [InlineData(IPStatus.BadDestination)]
        public void PingResult_FailedWithVariousStatuses_ShouldCreateCorrectResult(IPStatus status)
        {
            // Arrange
            var target = IPAddress.Parse("10.0.0.1");

            // Act
            var result = PingResult.Failed(target, status);

            // Assert
            Assert.False(result.success);
            Assert.Equal(status, result.Status);
            Assert.Equal(0, result.ResponseTime);
        }

        [Fact]
        public void PingResult_Deconstruction_ShouldWork()
        {
            // Arrange
            var target = IPAddress.Parse("172.16.0.1");
            var success = true;
            var responseTime = 25L;
            var status = IPStatus.Success;
            var timestamp = DateTime.UtcNow;

            // Act
            var result = new PingResult(target, success, responseTime, status, timestamp);
            var (deconTarget, deconSuccess, deconResponseTime, deconStatus, deconTimestamp) = result;

            // Assert
            Assert.Equal(target, deconTarget);
            Assert.Equal(success, deconSuccess);
            Assert.Equal(responseTime, deconResponseTime);
            Assert.Equal(status, deconStatus);
            Assert.Equal(timestamp, deconTimestamp);
        }

        #endregion

        #region PingMultipleAsync Tests

        [Fact]
        public async Task PingMultipleAsync_WithEmptyTargets_ShouldReturnEmptyResults()
        {
            // Arrange
            var targets = new List<IPAddress>();

            // Act
            var result = await PingService.PingMultipleAsync(targets);

            // Assert
            Assert.True(result.IsRight);
            result.IfRight(results => Assert.True(results.IsEmpty));
        }

        [Fact]
        public async Task PingMultipleAsync_WithSingleTarget_ShouldReturnSingleResult()
        {
            // Arrange
            var targets = new[] { IPAddress.Loopback };

            // Act
            var result = await PingService.PingMultipleAsync(targets);

            // Assert
            Assert.True(result.IsRight);
            result.IfRight(results =>
            {
                Assert.Single(results);
                Assert.Equal(IPAddress.Loopback, results.Head.Target);
            });
        }

        [Fact]
        public async Task PingMultipleAsync_WithMultipleTargets_ShouldReturnMultipleResults()
        {
            // Arrange
            var targets = new[]
            {
            IPAddress.Loopback,
            IPAddress.Parse("127.0.0.1"),
            IPAddress.Parse("::1")
        };

            // Act
            var result = await PingService.PingMultipleAsync(targets, maxConcurrency: 2);

            // Assert
            Assert.True(result.IsRight);
            result.IfRight(results =>
            {
                Assert.Equal(3, results.Count);
                Assert.All(results, r => Assert.Contains(r.Target, targets));
            });
        }

        [Fact]
        public async Task PingMultipleAsync_WithCustomConfig_ShouldUseConfig()
        {
            // Arrange
            var targets = new[] { IPAddress.Loopback };
            var config = new PingConfiguration(1000, 16, 32, false);

            // Act
            var result = await PingService.PingMultipleAsync(targets, config);

            // Assert
            Assert.True(result.IsRight);
            result.IfRight(results =>
            {
                Assert.Single(results);
            });
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(10)]
        public async Task PingMultipleAsync_WithDifferentConcurrency_ShouldWork(int maxConcurrency)
        {
            // Arrange
            var targets = Enumerable.Range(0, 5)
                .Select(_ => IPAddress.Loopback)
                .ToArray();

            // Act
            var result = await PingService.PingMultipleAsync(targets, maxConcurrency: maxConcurrency);

            // Assert
            Assert.True(result.IsRight);
            result.IfRight(results =>
            {
                Assert.Equal(5, results.Count);
            });
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task PingAsync_WithNullTarget_ShouldReturnError()
        {
            // Arrange
            IPAddress target = null;

            // Act
            var result = await PingService.PingAsync(target, PingConfiguration.Default);

            // Assert
            Assert.True(result.IsLeft);
            result.IfLeft(error =>
            {
                Assert.IsType<NetworkError.PingFailed>(error);
                var pingError = (NetworkError.PingFailed)error;
                Assert.Contains("Value cannot be null", pingError.ErrorMessage);
            });
        }

        [Fact]
        public async Task PingMultipleAsync_WithNullTargets_ShouldReturnError()
        {
            // Arrange
            IEnumerable<IPAddress> targets = null;

            // Act
            var result = await PingService.PingMultipleAsync(targets);

            // Assert
            Assert.True(result.IsLeft);
            result.IfLeft(error =>
            {
                Assert.IsType<NetworkError.BatchPingFailed>(error);
                var batchError = (NetworkError.BatchPingFailed)error;
                Assert.Contains("Value cannot be null", batchError.ErrorMessage);
            });
        }

        [Fact]
        public async Task TraceRouteAsync_WithNullTarget_ShouldReturnError()
        {
            // Arrange
            IPAddress target = null;

            // Act
            var result = await PingService.TraceRouteAsync(target);

            // Assert
            Assert.True(result.IsLeft);
            result.IfLeft(error =>
            {
                Assert.IsType<NetworkError.TraceRouteFailed>(error);
                var traceError = (NetworkError.TraceRouteFailed)error;
                Assert.Contains("Value cannot be null", traceError.ErrorMessage);
            });
        }

        #endregion

        #region Concurrency Tests

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(10)]
        public async Task PingMultipleAsync_WithDifferentConcurrencyLevels_ShouldWork(int maxConcurrency)
        {
            // Arrange
            var targets = Enumerable.Range(1, 10)
                .Select(i => IPAddress.Parse($"127.0.0.{i}"))
                .ToArray();

            // Act
            var result = await PingService.PingMultipleAsync(targets, maxConcurrency: maxConcurrency);

            // Assert
            Assert.True(result.IsRight);
            result.IfRight(results =>
            {
                Assert.Equal(10, results.Count);
                Assert.All(results, r => Assert.Contains(r.Target, targets));
            });
        }

        [Fact]
        public async Task PingMultipleAsync_WithZeroConcurrency_ShouldHandleGracefully()
        {
            // Arrange
            var targets = new[] { IPAddress.Loopback };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await PingService.PingMultipleAsync(targets, maxConcurrency: 0);
            });
        }

        [Fact]
        public async Task PingMultipleAsync_WithNegativeConcurrency_ShouldHandleGracefully()
        {
            // Arrange
            var targets = new[] { IPAddress.Loopback };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await PingService.PingMultipleAsync(targets, maxConcurrency: -1);
            });
        }

        #endregion

        #region Pattern Matching Tests

        [Fact]
        public async Task PingMultipleAsync_ResultPatternMatching_ShouldWork()
        {
            // Arrange
            var targets = new[] { IPAddress.Loopback };

            // Act
            var result = await PingService.PingMultipleAsync(targets);

            // Assert
            var count = result.Match(
                Right: results => results.Count,
                Left: _ => -1
            );

            Assert.Equal(1, count);
        }

        #endregion
    }
}
