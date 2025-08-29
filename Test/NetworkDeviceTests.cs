using NetworkAnalyzer;
using System.Net;
using System.Net.NetworkInformation;
using Xunit;

namespace Tests
{
    public class NetworkDeviceTests
    {

        [Fact]
        public void Create_ShouldInitializeWithDefaults()
        {
            // Arrange
            var ipAddress = IPAddress.Parse("192.168.1.1");

            // Act
            var device = NetworkDevice.Create(ipAddress);

            // Assert
            Assert.Equal(ipAddress, device.IpAddress);
            Assert.True(device.HostName.IsNone);
            Assert.True(device.MacAddress.IsNone);
            Assert.False(device.IsReachable);
            Assert.True(device.ResponseTime.IsNone);
            Assert.True(device.PingStatus.IsNone);
            Assert.Equal(NetworkDeviceType.Unknown, device.DeviceType);
        }

        [Fact]
        public void WithMethods_ShouldUpdateProperties()
        {
            // Arrange
            var ipAddress = IPAddress.Parse("10.0.0.1");
            var hostName = "test-device";
            var macAddress = PhysicalAddress.Parse("AA-BB-CC-DD-EE-FF");

            // Act
            var device = NetworkDevice.Create(ipAddress)
                .WithHostName(hostName)
                .WithMacAddress(macAddress)
                .WithPingResult(true, 25, IPStatus.Success)
                .WithDeviceType(NetworkDeviceType.Server);

            // Assert
            Assert.Equal(hostName, device.HostName.Match(Some: h => h, None: () => ""));
            Assert.Equal(macAddress, device.MacAddress.Match(Some: m => m, None: () => PhysicalAddress.None));
            Assert.True(device.IsReachable);
            Assert.Equal(25, device.ResponseTime.Match(Some: r => r, None: () => -1));
            Assert.Equal(IPStatus.Success, device.PingStatus.Match(Some: s => s, None: () => IPStatus.Unknown));
            Assert.Equal(NetworkDeviceType.Server, device.DeviceType);
        }

        [Theory]
        [InlineData("192.168.1.1", "router", "router (192.168.1.1)")]
        [InlineData("10.0.0.1", "", "10.0.0.1")]
        public void GetDisplayName_ShouldFormatCorrectly(string ipString, string hostName, string expected)
        {
            // Arrange
            var device = NetworkDevice.Create(IPAddress.Parse(ipString));
            if (!string.IsNullOrEmpty(hostName))
                device = device.WithHostName(hostName);

            // Act
            var displayName = device.GetDisplayName();

            // Assert
            Assert.Equal(expected, displayName);
        }

        [Fact]
        public void GetDisplayName_WithoutHostName_ShouldReturnIP()
        {
            // Arrange
            var ipAddress = IPAddress.Parse("172.16.0.1");
            var device = NetworkDevice.Create(ipAddress);

            // Act
            var displayName = device.GetDisplayName();

            // Assert
            Assert.Equal(ipAddress.ToString(), displayName);
        }

        [Fact]
        public void ToString_ShouldReturnDisplayName()
        {
            // Arrange
            var device = NetworkDevice.Create(IPAddress.Parse("8.8.8.8"))
                .WithHostName("google-dns");

            // Act & Assert
            Assert.Equal(device.GetDisplayName(), device.ToString());
        }

        [Fact]
        public void NetworkDevice_ShouldHaveValueEquality()
        {
            // Arrange
            var ip = IPAddress.Parse("192.168.1.1");
            var device1 = NetworkDevice.Create(ip).WithHostName("test");
            var device2 = NetworkDevice.Create(ip).WithHostName("test");
            var device3 = NetworkDevice.Create(ip).WithHostName("different");

            // Act & Assert
            Assert.Equal(device1, device2);
            Assert.NotEqual(device1, device3);
            Assert.Equal(device1.GetHashCode(), device2.GetHashCode());
        }
    }
}

