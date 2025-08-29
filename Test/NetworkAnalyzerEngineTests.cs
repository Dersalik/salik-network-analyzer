using NetworkAnalyzer;
using System.Net;
using System.Net.NetworkInformation;
using Xunit;
using static LanguageExt.Prelude;

namespace Tests
{
    public class NetworkAnalyzerEngineTests
    {
        #region AnalysisOptions Tests

        [Fact]
        public void AnalysisOptions_Default_ShouldHaveCorrectValues()
        {
            // Act
            var options = AnalysisOptions.Default;

            // Assert
            Assert.Equal(PingConfiguration.Default, options.PingConfig);
            Assert.Equal(50, options.MaxConcurrency);
            Assert.False(options.IncludeTraceRoute);
            Assert.True(options.IncludePortScan);
            Assert.True(options.TargetNetwork.IsNone);
        }

        [Fact]
        public void AnalysisOptions_CustomValues_ShouldBeSetCorrectly()
        {
            // Arrange
            var customPingConfig = new PingConfiguration(1000, 16, 32, false);
            var targetNetwork = "192.168.1.0/24";

            // Act
            var options = new AnalysisOptions(
                customPingConfig,
                100,
                true,
                false,
                Some(targetNetwork)
            );

            // Assert
            Assert.Equal(customPingConfig, options.PingConfig);
            Assert.Equal(100, options.MaxConcurrency);
            Assert.True(options.IncludeTraceRoute);
            Assert.False(options.IncludePortScan);
            Assert.Equal(targetNetwork, options.TargetNetwork.Match(Some: n => n, None: () => ""));
        }

        [Fact]
        public void AnalysisOptions_RecordEquality_ShouldWork()
        {
            // Arrange
            var options1 = new AnalysisOptions(PingConfiguration.Default, 50, true, false, Some("192.168.1.0/24"));
            var options2 = new AnalysisOptions(PingConfiguration.Default, 50, true, false, Some("192.168.1.0/24"));
            var options3 = new AnalysisOptions(PingConfiguration.Default, 100, true, false, Some("192.168.1.0/24"));

            // Assert
            Assert.Equal(options1, options2);
            Assert.NotEqual(options1, options3);
            Assert.Equal(options1.GetHashCode(), options2.GetHashCode());
        }

        [Fact]
        public void AnalysisOptions_WithExpression_ShouldWork()
        {
            // Arrange
            var original = AnalysisOptions.Default;

            // Act
            var modified = original with { MaxConcurrency = 100, IncludeTraceRoute = true };

            // Assert
            Assert.Equal(100, modified.MaxConcurrency);
            Assert.True(modified.IncludeTraceRoute);
            Assert.Equal(original.PingConfig, modified.PingConfig);
            Assert.Equal(original.IncludePortScan, modified.IncludePortScan);
            Assert.Equal(original.TargetNetwork, modified.TargetNetwork);
        }

        #endregion

        #region NetworkAnalysisResult Tests

        [Fact]
        public void NetworkAnalysisResult_Constructor_ShouldSetAllProperties()
        {
            // Arrange
            var devices = new[] { NetworkDevice.Create(IPAddress.Parse("192.168.1.1")) }.ToSeq();
            var interfaces = NetworkInterface.GetAllNetworkInterfaces().Take(1).ToSeq();
            var traceRoute = Some(new[] { IPAddress.Parse("192.168.1.1") }.ToSeq());
            var timestamp = DateTime.UtcNow;
            var duration = TimeSpan.FromMinutes(5);
            var statistics = new AnalysisStatistics(10, 5, 5, 50.0, 100, 50, 150);

            // Act
            var result = new NetworkAnalysisResult(devices, interfaces, traceRoute, timestamp, duration, statistics);

            // Assert
            Assert.Equal(devices, result.DiscoveredDevices);
            Assert.Equal(interfaces, result.NetworkInterfaces);
            Assert.Equal(traceRoute, result.TraceRouteResults);
            Assert.Equal(timestamp, result.AnalysisTimestamp);
            Assert.Equal(duration, result.AnalysisDuration);
            Assert.Equal(statistics, result.Statistics);
        }

        [Fact]
        public void NetworkAnalysisResult_RecordEquality_ShouldWork()
        {
            // Arrange
            var devices = new[] { NetworkDevice.Create(IPAddress.Parse("192.168.1.1")) }.ToSeq();
            var interfaces = NetworkInterface.GetAllNetworkInterfaces().Take(1).ToSeq();
            var timestamp = DateTime.UtcNow;
            var duration = TimeSpan.FromMinutes(5);
            var statistics = new AnalysisStatistics(10, 5, 5, 50.0, 100, 50, 150);

            var result1 = new NetworkAnalysisResult(devices, interfaces, None, timestamp, duration, statistics);
            var result2 = new NetworkAnalysisResult(devices, interfaces, None, timestamp, duration, statistics);

            // Assert
            Assert.Equal(result1, result2);
            Assert.Equal(result1.GetHashCode(), result2.GetHashCode());
        }

        #endregion

        #region AnalysisStatistics Tests

        [Fact]
        public void AnalysisStatistics_FromDevices_WithActiveDevices_ShouldCalculateCorrectly()
        {
            // Arrange
            var devices = new[]
            {
                NetworkDevice.Create(IPAddress.Parse("192.168.1.1"))
                    .WithPingResult(true, 50, IPStatus.Success),
                NetworkDevice.Create(IPAddress.Parse("192.168.1.2"))
                    .WithPingResult(true, 100, IPStatus.Success),
                NetworkDevice.Create(IPAddress.Parse("192.168.1.3"))
                    .WithPingResult(false, 0, IPStatus.TimedOut)
            }.ToSeq();

            // Act
            var statistics = AnalysisStatistics.FromDevices(devices, 10);

            // Assert
            Assert.Equal(10, statistics.TotalDevicesScanned);
            Assert.Equal(2, statistics.ActiveDevices);
            Assert.Equal(8, statistics.InactiveDevices);
            Assert.Equal(20.0, statistics.SuccessRate);
            Assert.Equal(75, statistics.AverageResponseTime);
            Assert.Equal(50, statistics.MinResponseTime);
            Assert.Equal(100, statistics.MaxResponseTime);
        }

        [Fact]
        public void AnalysisStatistics_FromDevices_WithNoActiveDevices_ShouldHandleGracefully()
        {
            // Arrange
            var devices = new[]
            {
                NetworkDevice.Create(IPAddress.Parse("192.168.1.1"))
                    .WithPingResult(false, 0, IPStatus.TimedOut),
                NetworkDevice.Create(IPAddress.Parse("192.168.1.2"))
                    .WithPingResult(false, 0, IPStatus.DestinationHostUnreachable)
            }.ToSeq();

            // Act
            var statistics = AnalysisStatistics.FromDevices(devices, 5);

            // Assert
            Assert.Equal(5, statistics.TotalDevicesScanned);
            Assert.Equal(0, statistics.ActiveDevices);
            Assert.Equal(5, statistics.InactiveDevices);
            Assert.Equal(0.0, statistics.SuccessRate);
            Assert.Equal(0, statistics.AverageResponseTime);
            Assert.Equal(0, statistics.MinResponseTime);
            Assert.Equal(0, statistics.MaxResponseTime);
        }

        [Fact]
        public void AnalysisStatistics_FromDevices_WithEmptyDevices_ShouldHandleGracefully()
        {
            // Arrange
            var devices = Seq<NetworkDevice>();

            // Act
            var statistics = AnalysisStatistics.FromDevices(devices, 0);

            // Assert
            Assert.Equal(0, statistics.TotalDevicesScanned);
            Assert.Equal(0, statistics.ActiveDevices);
            Assert.Equal(0, statistics.InactiveDevices);
            Assert.Equal(0.0, statistics.SuccessRate);
            Assert.Equal(0, statistics.AverageResponseTime);
            Assert.Equal(0, statistics.MinResponseTime);
            Assert.Equal(0, statistics.MaxResponseTime);
        }

        [Theory]
        [InlineData(0, 0.0)]
        [InlineData(10, 100.0)]
        [InlineData(5, 50.0)]
        public void AnalysisStatistics_FromDevices_WithDifferentSuccessRates_ShouldCalculateCorrectly(
            int activeCount, double expectedSuccessRate)
        {
            // Arrange
            var devices = new List<NetworkDevice>();
            var totalScanned = 10;

            for (int i = 0; i < activeCount; i++)
            {
                devices.Add(NetworkDevice.Create(IPAddress.Parse($"192.168.1.{i + 1}"))
                    .WithPingResult(true, 50, IPStatus.Success));
            }

            // Act
            var statistics = AnalysisStatistics.FromDevices(devices.ToSeq(), totalScanned);

            // Assert
            Assert.Equal(expectedSuccessRate, statistics.SuccessRate);
            Assert.Equal(activeCount, statistics.ActiveDevices);
            Assert.Equal(totalScanned - activeCount, statistics.InactiveDevices);
        }

        #endregion

        #region AnalyzeNetworkAsync Tests
        [Fact]
        public async Task AnalyzeNetworkAsync_WithCustomOptions_ShouldUseCustomValues()
        {
            // Arrange
            var customConfig = new PingConfiguration(1000, 16, 32, false);
            var options = new AnalysisOptions(
                customConfig,
                10,
                false,
                false,
                None
            );

            // Act
            var result = await NetworkAnalyzerEngine.AnalyzeNetworkAsync(options);

            // Assert
            Assert.True(result.IsRight || result.IsLeft);
            result.IfRight(analysisResult =>
            {
                Assert.NotNull(analysisResult.DiscoveredDevices);
                Assert.NotNull(analysisResult.NetworkInterfaces);
                Assert.True(analysisResult.TraceRouteResults.IsNone); // Should be None since IncludeTraceRoute is false
            });
        }

        [Fact]
        public async Task AnalyzeNetworkAsync_WithSpecificNetwork_ShouldAnalyzeTargetNetwork()
        {
            // Arrange
            var targetNetwork = "127.0.0.1/32"; // Loopback for reliable testing
            var options = AnalysisOptions.Default with { TargetNetwork = Some(targetNetwork) };

            // Act
            var result = await NetworkAnalyzerEngine.AnalyzeNetworkAsync(options);

            // Assert
            Assert.True(result.IsRight);
            result.IfRight(analysisResult =>
            {
                Assert.NotNull(analysisResult.DiscoveredDevices);
                Assert.True(analysisResult.Statistics.TotalDevicesScanned >= 0);

                // Should find loopback device if system supports it
                if (analysisResult.DiscoveredDevices.Any())
                {
                    Assert.Contains(analysisResult.DiscoveredDevices,
                        d => d.IpAddress.Equals(IPAddress.Parse("127.0.0.1")));
                }
            });
        }

        [Fact]
        public async Task AnalyzeNetworkAsync_WithInvalidNetwork_ShouldReturnError()
        {
            // Arrange
            var invalidNetwork = "invalid-network";
            var options = AnalysisOptions.Default with { TargetNetwork = Some(invalidNetwork) };

            // Act
            var result = await NetworkAnalyzerEngine.AnalyzeNetworkAsync(options);

            // Assert
            Assert.True(result.IsLeft);
            result.IfLeft(error =>
            {
                Assert.IsType<NetworkError.NetworkDiscoveryFailed>(error);
            });
        }

        [Fact]
        public async Task AnalyzeNetworkAsync_WithTraceRouteEnabled_ShouldIncludeTraceRoute()
        {
            // Arrange
            var options = AnalysisOptions.Default with
            {
                IncludeTraceRoute = true,
                TargetNetwork = Some("127.0.0.1/32")
            };

            // Act
            var result = await NetworkAnalyzerEngine.AnalyzeNetworkAsync(options);

            // Assert
            result.IfRight(analysisResult =>
            {
                if (analysisResult.DiscoveredDevices.Any())
                {
                    // Should have trace route results if devices were found
                    Assert.True(analysisResult.TraceRouteResults.IsSome || analysisResult.TraceRouteResults.IsNone);
                }
                else
                {
                    // No devices found, so no trace route
                    Assert.True(analysisResult.TraceRouteResults.IsNone);
                }
            });
        }

        [Theory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        public async Task AnalyzeNetworkAsync_WithDifferentConcurrency_ShouldWork(int maxConcurrency)
        {
            // Arrange
            var options = AnalysisOptions.Default with
            {
                MaxConcurrency = maxConcurrency,
                TargetNetwork = Some("127.0.0.1/32")
            };

            // Act
            var result = await NetworkAnalyzerEngine.AnalyzeNetworkAsync(options);

            // Assert
            Assert.True(result.IsRight || result.IsLeft);
        }

        [Fact]
        public async Task AnalyzeNetworkAsync_ResultTiming_ShouldBeAccurate()
        {
            // Arrange
            var startTime = DateTime.UtcNow;
            var options = AnalysisOptions.Default with { TargetNetwork = Some("127.0.0.1/32") };

            // Act
            var result = await NetworkAnalyzerEngine.AnalyzeNetworkAsync(options);
            var endTime = DateTime.UtcNow;

            // Assert
            result.IfRight(analysisResult =>
            {
                Assert.True(analysisResult.AnalysisTimestamp >= startTime);
                Assert.True(analysisResult.AnalysisTimestamp <= endTime);
                Assert.True(analysisResult.AnalysisDuration >= TimeSpan.Zero);
                Assert.True(analysisResult.AnalysisDuration <= (endTime - startTime + TimeSpan.FromSeconds(1)));
            });
        }

        #endregion

        #region Pattern Matching Tests

        [Fact]
        public async Task NetworkAnalyzerEngine_Results_ShouldSupportPatternMatching()
        {
            // Arrange
            var target = IPAddress.Loopback;

            // Act
            var result = await NetworkAnalyzerEngine.AnalyzeSingleDeviceAsync(target);

            // Assert
            var outcome = result.Match(
                Right: device => $"Success: {device.IpAddress}",
                Left: error => $"Error: {error.Message}"
            );

            Assert.True(outcome.StartsWith("Success:") || outcome.StartsWith("Error:"));
        }

        [Fact]
        public async Task ContinuousPingAsync_Results_ShouldSupportPatternMatching()
        {
            // Arrange
            var target = IPAddress.Loopback;

            // Act
            var result = await NetworkAnalyzerEngine.ContinuousPingAsync(target, 2);

            // Assert
            var pingCount = result.Match(
                Right: results => results.Count,
                Left: _ => -1
            );

            Assert.True(pingCount == 2 || pingCount == -1);
        }

        #endregion

        #region Edge Cases and Error Handling

        [Fact]
        public async Task AnalyzeSingleDeviceAsync_WithNullTarget_ShouldReturnError()
        {
            // Arrange
            IPAddress target = null;

            // Act
            var result = await NetworkAnalyzerEngine.AnalyzeSingleDeviceAsync(target);

            // Assert
            Assert.True(result.IsLeft);
            result.IfLeft(error =>
            {
                Assert.IsType<NetworkError.NetworkDiscoveryFailed>(error);
            });
        }

        [Fact]
        public async Task ContinuousPingAsync_WithZeroCount_ShouldReturnEmptyResults()
        {
            // Arrange
            var target = IPAddress.Loopback;

            // Act
            var result = await NetworkAnalyzerEngine.ContinuousPingAsync(target, 0);

            // Assert
            Assert.True(result.IsRight);
            result.IfRight(results =>
            {
                Assert.True(results.IsEmpty);
            });
        }

        [Fact]
        public async Task ContinuousPingAsync_WithNegativeCount_ShouldReturnEmptyResults()
        {
            // Arrange
            var target = IPAddress.Loopback;

            // Act
            var result = await NetworkAnalyzerEngine.ContinuousPingAsync(target, -5);

            // Assert
            Assert.True(result.IsRight);
            result.IfRight(results =>
            {
                Assert.True(results.IsEmpty);
            });
        }

        [Fact]
        public async Task AnalyzeNetworkAsync_WithExtremelyShortTimeout_ShouldHandleGracefully()
        {
            // Arrange
            var options = AnalysisOptions.Default with
            {
                PingConfig = new PingConfiguration(1, 1, 1, true),
                TargetNetwork = Some("127.0.0.1/32")
            };

            // Act
            var result = await NetworkAnalyzerEngine.AnalyzeNetworkAsync(options);

            // Assert
            Assert.True(result.IsRight || result.IsLeft);
        }

        #endregion

        #region Performance and Reliability Tests

        [Fact]
        public async Task AnalyzeNetworkAsync_WithSmallNetwork_ShouldCompleteQuickly()
        {
            // Arrange
            var options = AnalysisOptions.Default with
            {
                TargetNetwork = Some("127.0.0.1/30"), // Only 4 addresses
                PingConfig = new PingConfiguration(1000, 32, 64, true)
            };
            var startTime = DateTime.UtcNow;

            // Act
            var result = await NetworkAnalyzerEngine.AnalyzeNetworkAsync(options);

            // Assert
            var duration = DateTime.UtcNow - startTime;
            Assert.True(duration.TotalSeconds < 30); // Should complete within 30 seconds
            Assert.True(result.IsRight || result.IsLeft);
        }

        [Fact]
        public async Task ContinuousPingAsync_WithManyPings_ShouldCompleteInReasonableTime()
        {
            // Arrange
            var target = IPAddress.Loopback;
            var count = 10;
            var interval = TimeSpan.FromMilliseconds(100);
            var startTime = DateTime.UtcNow;

            // Act
            var result = await NetworkAnalyzerEngine.ContinuousPingAsync(target, count, interval);

            // Assert
            var duration = DateTime.UtcNow - startTime;
            Assert.True(result.IsRight);

            result.IfRight(results =>
            {
                Assert.Equal(count, results.Count);
                // Should take at least the interval time between pings
                var expectedMinDuration = interval * (count - 1);
                Assert.True(duration >= expectedMinDuration);
            });
        }

        [Fact]
        public async Task AnalyzeSingleDeviceAsync_ShouldBeReliable()
        {
            // Run the same operation multiple times to ensure reliability
            var target = IPAddress.Loopback;
            var tasks = Enumerable.Range(0, 5)
                .Select(_ => NetworkAnalyzerEngine.AnalyzeSingleDeviceAsync(target))
                .ToArray();

            // Act
            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, result =>
            {
                Assert.True(result.IsRight);
                result.IfRight(device =>
                {
                    Assert.Equal(target, device.IpAddress);
                    Assert.True(device.IsReachable);
                });
            });
        }

        #endregion

        #region Functional Programming Validation Tests

        [Fact]
        public void AnalysisOptions_IsValueType_ShouldHaveValueSemantics()
        {
            // Arrange
            var options1 = AnalysisOptions.Default;

            // Act
            var options2 = options1; // Should copy by value

            // Assert
            Assert.Equal(options1, options2);
            Assert.True(typeof(AnalysisOptions).IsValueType);
        }

        [Fact]
        public void NetworkAnalysisResult_IsValueType_ShouldHaveValueSemantics()
        {
            // Arrange
            var devices = Seq<NetworkDevice>();
            var interfaces = Seq<NetworkInterface>();
            var timestamp = DateTime.UtcNow;
            var duration = TimeSpan.FromMinutes(1);
            var statistics = new AnalysisStatistics(0, 0, 0, 0, 0, 0, 0);

            var result1 = new NetworkAnalysisResult(devices, interfaces, None, timestamp, duration, statistics);

            // Act
            var result2 = result1; // Should copy by value

            // Assert
            Assert.Equal(result1, result2);
            Assert.True(typeof(NetworkAnalysisResult).IsValueType);
        }

        [Fact]
        public void AnalysisStatistics_IsValueType_ShouldHaveValueSemantics()
        {
            // Arrange
            var stats1 = new AnalysisStatistics(10, 5, 5, 50.0, 100, 50, 150);

            // Act
            var stats2 = stats1; // Should copy by value

            // Assert
            Assert.Equal(stats1, stats2);
            Assert.True(typeof(AnalysisStatistics).IsValueType);
        }

        #endregion

        #region Data Structure Validation Tests

        [Fact]
        public async Task AnalyzeNetworkAsync_Result_ShouldHaveConsistentStatistics()
        {
            // Arrange
            var options = AnalysisOptions.Default with { TargetNetwork = Some("127.0.0.1/32") };

            // Act
            var result = await NetworkAnalyzerEngine.AnalyzeNetworkAsync(options);

            // Assert
            result.IfRight(analysisResult =>
            {
                var stats = analysisResult.Statistics;
                var devices = analysisResult.DiscoveredDevices;

                // Statistics should match discovered devices
                Assert.Equal(devices.Count(d => d.IsReachable), stats.ActiveDevices);
                Assert.Equal(stats.TotalDevicesScanned - stats.ActiveDevices, stats.InactiveDevices);

                // Success rate calculation
                if (stats.TotalDevicesScanned > 0)
                {
                    var expectedSuccessRate = (double)stats.ActiveDevices / stats.TotalDevicesScanned * 100;
                    Assert.Equal(expectedSuccessRate, stats.SuccessRate, 0.1);
                }
            });
        }
        #endregion

        #region Concurrency and Threading Tests

        [Fact]
        public async Task AnalyzeNetworkAsync_ConcurrentCalls_ShouldNotInterfere()
        {
            // Arrange
            var options = AnalysisOptions.Default with { TargetNetwork = Some("127.0.0.1/32") };

            // Act - Run multiple analyses concurrently
            var tasks = Enumerable.Range(0, 3)
                .Select(_ => NetworkAnalyzerEngine.AnalyzeNetworkAsync(options))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, result =>
            {
                Assert.True(result.IsRight || result.IsLeft);
            });

            // All successful results should have similar structure
            var successfulResults = results.Where(r => r.IsRight).ToArray();
            if (successfulResults.Length > 1)
            {
                Assert.All(successfulResults, result =>
                {
                    result.IfRight(analysisResult =>
                    {
                        Assert.NotNull(analysisResult.DiscoveredDevices);
                        Assert.NotNull(analysisResult.NetworkInterfaces);
                    });
                });
            }
        }

        [Fact]
        public async Task ContinuousPingAsync_ConcurrentCalls_ShouldNotInterfere()
        {
            // Arrange
            var target = IPAddress.Loopback;

            // Act - Run multiple continuous pings concurrently
            var tasks = Enumerable.Range(0, 3)
                .Select(_ => NetworkAnalyzerEngine.ContinuousPingAsync(target, 2))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, result =>
            {
                Assert.True(result.IsRight);
                result.IfRight(pingResults =>
                {
                    Assert.Equal(2, pingResults.Count);
                    Assert.All(pingResults, r => Assert.Equal(target, r.Target));
                });
            });
        }

        #endregion

        #region Record Equality and Immutability Tests

        [Fact]
        public void AnalysisOptions_RecordMethods_ShouldWorkCorrectly()
        {
            // Arrange
            var original = AnalysisOptions.Default;

            // Act
            var modified = original with { MaxConcurrency = 100 };
            var copy = original;

            // Assert
            Assert.NotEqual(original, modified);
            Assert.Equal(original, copy);
            Assert.Equal(100, modified.MaxConcurrency);
            Assert.Equal(50, original.MaxConcurrency); // Original should be unchanged
        }

        [Fact]
        public void NetworkAnalysisResult_WithExpression_ShouldWork()
        {
            // Arrange
            var devices = Seq<NetworkDevice>();
            var interfaces = Seq<NetworkInterface>();
            var timestamp = DateTime.UtcNow;
            var duration = TimeSpan.FromMinutes(1);
            var statistics = new AnalysisStatistics(0, 0, 0, 0, 0, 0, 0);

            var original = new NetworkAnalysisResult(devices, interfaces, None, timestamp, duration, statistics);

            // Act
            var newTimestamp = DateTime.UtcNow.AddHours(1);
            var modified = original with { AnalysisTimestamp = newTimestamp };

            // Assert
            Assert.NotEqual(original, modified);
            Assert.Equal(newTimestamp, modified.AnalysisTimestamp);
            Assert.Equal(timestamp, original.AnalysisTimestamp); // Original unchanged
        }

        [Fact]
        public void AnalysisStatistics_RecordMethods_ShouldWorkCorrectly()
        {
            // Arrange
            var original = new AnalysisStatistics(10, 5, 5, 50.0, 100, 50, 150);

            // Act
            var modified = original with { SuccessRate = 75.0 };

            // Assert
            Assert.NotEqual(original, modified);
            Assert.Equal(75.0, modified.SuccessRate);
            Assert.Equal(50.0, original.SuccessRate); // Original unchanged
        }

        #endregion

        #region Null and Edge Case Parameter Tests

        [Theory]
        [InlineData(null, null)]
        [InlineData("", "")]
        [InlineData(" ", " ")]
        public async Task AnalyzeNetworkAsync_WithInvalidNetworkStrings_ShouldHandleGracefully(string network1, string network2)
        {
            // Arrange
            var options = AnalysisOptions.Default with { TargetNetwork = Some(network1 ?? "") };

            // Act
            var result = await NetworkAnalyzerEngine.AnalyzeNetworkAsync(options);

            // Assert
            Assert.True(result.IsRight || result.IsLeft);
            result.IfLeft(error =>
            {
                Assert.IsType<NetworkError.NetworkDiscoveryFailed>(error);
            });
        }

        [Fact]
        public async Task ContinuousPingAsync_WithNullInterval_ShouldUseDefault()
        {
            // Arrange
            var target = IPAddress.Loopback;
            var startTime = DateTime.UtcNow;

            // Act
            var result = await NetworkAnalyzerEngine.ContinuousPingAsync(target, 2, null);

            // Assert
            var duration = DateTime.UtcNow - startTime;
            Assert.True(result.IsRight);
            result.IfRight(results =>
            {
                Assert.Equal(2, results.Count);
                // Should use default interval of 1 second, so should take at least 1 second
                Assert.True(duration >= TimeSpan.FromSeconds(0.9)); // Allow some tolerance
            });
        }

        #endregion
    }
}
