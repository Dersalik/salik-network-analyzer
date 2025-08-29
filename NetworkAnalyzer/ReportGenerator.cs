using LanguageExt;
using System.Text;

namespace NetworkAnalyzer;

public enum ReportFormat
{
    Console,
    Json,
    Csv,
    Markdown
}
public static class ReportGenerator
{
    public static string GenerateReport(NetworkAnalysisResult result, ReportFormat format = ReportFormat.Console)
    {
        return format switch
        {
            ReportFormat.Console => GenerateConsoleReport(result),
            ReportFormat.Json => GenerateJsonReport(result),
            ReportFormat.Csv => GenerateCsvReport(result),
            ReportFormat.Markdown => GenerateMarkdownReport(result),
            _ => GenerateConsoleReport(result)
        };
    }

    public static string GenerateDeviceSummary(NetworkDevice device, ReportFormat format = ReportFormat.Console)
    {
        return format switch
        {
            ReportFormat.Console => GenerateConsoleDeviceSummary(device),
            ReportFormat.Json => GenerateJsonDeviceSummary(device),
            ReportFormat.Csv => GenerateCsvDeviceSummary(device),
            ReportFormat.Markdown => GenerateMarkdownDeviceSummary(device),
            _ => GenerateConsoleDeviceSummary(device)
        };
    }

    private static string GenerateConsoleReport(NetworkAnalysisResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔══════════════════════════════════════╗");
        sb.AppendLine("║         Network Analysis Report      ║");
        sb.AppendLine("╚══════════════════════════════════════╝");
        sb.AppendLine();

        sb.AppendLine($"Analysis Date: {result.AnalysisTimestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Duration: {result.AnalysisDuration.TotalSeconds:F2} seconds");
        sb.AppendLine();

        sb.AppendLine("═══ Analysis Statistics ═══");
        sb.AppendLine($"Total Devices Scanned: {result.Statistics.TotalDevicesScanned}");
        sb.AppendLine($"Active Devices: {result.Statistics.ActiveDevices}");
        sb.AppendLine($"Inactive Devices: {result.Statistics.InactiveDevices}");
        sb.AppendLine($"Success Rate: {result.Statistics.SuccessRate:F1}%");
        sb.AppendLine($"Average Response Time: {result.Statistics.AverageResponseTime}ms");
        sb.AppendLine($"Min Response Time: {result.Statistics.MinResponseTime}ms");
        sb.AppendLine($"Max Response Time: {result.Statistics.MaxResponseTime}ms");
        sb.AppendLine();

        sb.AppendLine("═══ Network Interfaces ═══");
        foreach (var netInterface in result.NetworkInterfaces)
        {
            sb.AppendLine($"• {netInterface.Name} ({netInterface.NetworkInterfaceType})");
            sb.AppendLine($"  Status: {netInterface.OperationalStatus}");
            sb.AppendLine($"  Speed: {netInterface.Speed / 1_000_000} Mbps");
            sb.AppendLine();
        }

        sb.AppendLine("═══ Discovered Devices ═══");
        var devicesByType = result.DiscoveredDevices
            .GroupBy(d => d.DeviceType)
            .OrderBy(g => g.Key.ToString());

        foreach (var group in devicesByType)
        {
            sb.AppendLine($"┌─ {group.Key} Devices ({group.Count()}) ─┐");
            foreach (var device in group.OrderBy(d => d.IpAddress.ToString()))
            {
                var status = device.IsReachable ? "✓" : "✗";
                var responseTime = device.ResponseTime.Match(
                    Some: time => $"{time}ms",
                    None: () => "N/A"
                );

                sb.AppendLine($"│ {status} {device.GetDisplayName()}");
                sb.AppendLine($"│   Response Time: {responseTime}");

                device.MacAddress.IfSome(mac =>
                    sb.AppendLine($"│   MAC Address: {mac}"));

                device.PingStatus.IfSome(status =>
                    sb.AppendLine($"│   Status: {status}"));

                sb.AppendLine("│");
            }
            sb.AppendLine("└─────────────────────────────────────┘");
            sb.AppendLine();
        }

        result.TraceRouteResults.IfSome(hops =>
        {
            sb.AppendLine("═══ Trace Route Results ═══");
            for (int i = 0; i < hops.Count; i++)
            {
                sb.AppendLine($"{i + 1:D2}. {hops[i]}");
            }
            sb.AppendLine();
        });

        return sb.ToString();
    }

    private static string GenerateJsonReport(NetworkAnalysisResult result)
    {
        var devices = result.DiscoveredDevices.Select(d => new
        {
            ipAddress = d.IpAddress.ToString(),
            hostName = d.HostName.Match(Some: n => n, None: () => string.Empty),
            macAddress = d.MacAddress.Match(Some: m => m.ToString(), None: () => string.Empty),
            isReachable = d.IsReachable,
            responseTime = d.ResponseTime.Match(Some: t => (long?)t, None: () => (long?)null),
            deviceType = d.DeviceType.ToString(),
            pingStatus = d.PingStatus.Match(Some: s => s.ToString(), None: () => string.Empty)
        });

        var interfaces = result.NetworkInterfaces.Select(ni => new
        {
            name = ni.Name,
            type = ni.NetworkInterfaceType.ToString(),
            status = ni.OperationalStatus.ToString(),
            speed = ni.Speed
        });

        var report = new
        {
            analysisTimestamp = result.AnalysisTimestamp,
            analysisDuration = result.AnalysisDuration.TotalSeconds,
            statistics = new
            {
                totalDevicesScanned = result.Statistics.TotalDevicesScanned,
                activeDevices = result.Statistics.ActiveDevices,
                inactiveDevices = result.Statistics.InactiveDevices,
                successRate = result.Statistics.SuccessRate,
                averageResponseTime = result.Statistics.AverageResponseTime,
                minResponseTime = result.Statistics.MinResponseTime,
                maxResponseTime = result.Statistics.MaxResponseTime
            },
            networkInterfaces = interfaces,
            discoveredDevices = devices,
            traceRouteResults = result.TraceRouteResults
             .Map(res => res.Map(ip => ip.ToString()).ToArray())
             .Match(
                  Some: hops => hops,
                    None: () => Array.Empty<string>()
                  )
        };
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return string.Empty;
        }
    }

    private static string GenerateCsvReport(NetworkAnalysisResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("IP Address,Host Name,MAC Address,Is Reachable,Response Time (ms),Device Type,Ping Status");

        foreach (var device in result.DiscoveredDevices)
        {
            var hostName = device.HostName.Match(Some: n => n, None: () => "");
            var macAddress = device.MacAddress.Match(Some: m => m.ToString(), None: () => "");
            var responseTime = device.ResponseTime.Match(Some: t => t.ToString(), None: () => "");
            var pingStatus = device.PingStatus.Match(Some: s => s.ToString(), None: () => "");

            sb.AppendLine($"{device.IpAddress},{hostName},{macAddress},{device.IsReachable},{responseTime},{device.DeviceType},{pingStatus}");
        }

        return sb.ToString();
    }

    private static string GenerateMarkdownReport(NetworkAnalysisResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Network Analysis Report");
        sb.AppendLine();
        sb.AppendLine($"**Analysis Date:** {result.AnalysisTimestamp:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Duration:** {result.AnalysisDuration.TotalSeconds:F2} seconds");
        sb.AppendLine();

        sb.AppendLine("## Analysis Statistics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("| --- | --- |");
        sb.AppendLine($"| Total Devices Scanned | {result.Statistics.TotalDevicesScanned} |");
        sb.AppendLine($"| Active Devices | {result.Statistics.ActiveDevices} |");
        sb.AppendLine($"| Inactive Devices | {result.Statistics.InactiveDevices} |");
        sb.AppendLine($"| Success Rate | {result.Statistics.SuccessRate:F1}% |");
        sb.AppendLine($"| Average Response Time | {result.Statistics.AverageResponseTime}ms |");
        sb.AppendLine($"| Min Response Time | {result.Statistics.MinResponseTime}ms |");
        sb.AppendLine($"| Max Response Time | {result.Statistics.MaxResponseTime}ms |");
        sb.AppendLine();

        sb.AppendLine("## Discovered Devices");
        sb.AppendLine();
        sb.AppendLine("| IP Address | Host Name | Status | Response Time | Device Type |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");

        foreach (var device in result.DiscoveredDevices.OrderBy(d => d.IpAddress.ToString()))
        {
            var hostName = device.HostName.Match(Some: n => n, None: () => "-");
            var status = device.IsReachable ? "✅ Online" : "❌ Offline";
            var responseTime = device.ResponseTime.Match(Some: t => $"{t}ms", None: () => "-");

            sb.AppendLine($"| {device.IpAddress} | {hostName} | {status} | {responseTime} | {device.DeviceType} |");
        }

        return sb.ToString();
    }

    private static string GenerateConsoleDeviceSummary(NetworkDevice device)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Device: {device.GetDisplayName()}");
        sb.AppendLine($"Status: {(device.IsReachable ? "Online" : "Offline")}");

        device.ResponseTime.IfSome(time =>
            sb.AppendLine($"Response Time: {time}ms"));

        device.MacAddress.IfSome(mac =>
            sb.AppendLine($"MAC Address: {mac}"));

        sb.AppendLine($"Device Type: {device.DeviceType}");

        device.PingStatus.IfSome(status =>
            sb.AppendLine($"Ping Status: {status}"));

        return sb.ToString();
    }

    private static string GenerateJsonDeviceSummary(NetworkDevice device)
    {
        var deviceInfo = new
        {
            ipAddress = device.IpAddress.ToString(),
            hostName = device.HostName.Match(Some: n => n, None: () => string.Empty),
            macAddress = device.MacAddress.Match(Some: m => m.ToString(), None: () => string.Empty),
            isReachable = device.IsReachable,
            responseTime = device.ResponseTime.Match(Some: t => (long?)t, None: () => (long?)null),
            deviceType = device.DeviceType.ToString(),
            pingStatus = device.PingStatus.Match(Some: s => s.ToString(), None: () => string.Empty)
        };

        return System.Text.Json.JsonSerializer.Serialize(deviceInfo, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static string GenerateCsvDeviceSummary(NetworkDevice device)
    {
        var hostName = device.HostName.Match(Some: n => n, None: () => "");
        var macAddress = device.MacAddress.Match(Some: m => m.ToString(), None: () => "");
        var responseTime = device.ResponseTime.Match(Some: t => t.ToString(), None: () => "");
        var pingStatus = device.PingStatus.Match(Some: s => s.ToString(), None: () => "");

        return $"{device.IpAddress},{hostName},{macAddress},{device.IsReachable},{responseTime},{device.DeviceType},{pingStatus}";
    }

    private static string GenerateMarkdownDeviceSummary(NetworkDevice device)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## Device: {device.GetDisplayName()}");
        sb.AppendLine();
        sb.AppendLine($"**Status:** {(device.IsReachable ? "🟢 Online" : "🔴 Offline")}  ");

        device.ResponseTime.IfSome(time =>
            sb.AppendLine($"**Response Time:** {time}ms  "));

        device.MacAddress.IfSome(mac =>
            sb.AppendLine($"**MAC Address:** {mac}  "));

        sb.AppendLine($"**Device Type:** {device.DeviceType}  ");

        device.PingStatus.IfSome(status =>
            sb.AppendLine($"**Ping Status:** {status}  "));

        return sb.ToString();
    }
}
