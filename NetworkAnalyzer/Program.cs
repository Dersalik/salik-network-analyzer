using LanguageExt;
using LanguageExt.UnsafeValueAccess;
using NetworkAnalyzer;
using System.Net;
using static LanguageExt.Prelude;

// Display startup banner
Console.WriteLine("╔═══════════════════════════════════════════╗");
Console.WriteLine("║          Network Analyzer v1.0           ║");
Console.WriteLine("║        Built with .NET 8 & language-ext  ║");
Console.WriteLine("╚═══════════════════════════════════════════╝");
Console.WriteLine();

// Main program logic - handle command line args or run interactive mode
if (args.Length == 0)
{
    return await RunInteractiveMode();
}

return await RunCommandLineMode(args);

// Local function definitions (must be static in top-level programs)
static async Task<int> RunInteractiveMode()
{
    while (true)
    {
        DisplayMainMenu();
        var choice = Console.ReadLine()?.Trim();

        var resultTask = choice switch
        {
            "1" => AnalyzeLocalNetwork(),
            "2" => AnalyzeSpecificNetwork(),
            "3" => PingSingleDevice(),
            "4" => ContinuousPing(),
            "5" => TraceRoute(),
            "6" => ExportResults(),
            "7" => Task.FromResult(Some(0)),
            _ => Task.FromResult<Option<int>>(None)
        };

        var result = await resultTask;

        var exitCode = result.Match(
            Some: code => code,
            None: () => -1  // Continue the loop test
        );

        if (exitCode != -1)
        {
            return exitCode;
        }

        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        Console.Clear();
    }
}

static async Task<int> RunCommandLineMode(string[] args)
{
    var command = args[0].ToLowerInvariant();

    return command switch
    {
        "scan" when args.Length >= 2 => await ScanNetwork(args[1]),
        "ping" when args.Length >= 2 => await PingDevice(args[1]),
        "trace" when args.Length >= 2 => await TraceRouteDevice(args[1]),
        "local" => await AnalyzeLocalNetworkCommand(),
        _ => ShowUsage()
    };
}

static void DisplayMainMenu()
{
    Console.WriteLine("╔═══════════════════════════════════════════╗");
    Console.WriteLine("║              Main Menu                    ║");
    Console.WriteLine("╠═══════════════════════════════════════════╣");
    Console.WriteLine("║ 1. Analyze Local Network                 ║");
    Console.WriteLine("║ 2. Analyze Specific Network Range        ║");
    Console.WriteLine("║ 3. Ping Single Device                    ║");
    Console.WriteLine("║ 4. Continuous Ping                       ║");
    Console.WriteLine("║ 5. Trace Route                           ║");
    Console.WriteLine("║ 6. Export Results                        ║");
    Console.WriteLine("║ 7. Exit                                  ║");
    Console.WriteLine("╚═══════════════════════════════════════════╝");
    Console.Write("Select an option (1-7): ");
}

static async Task<Option<int>> AnalyzeLocalNetwork()
{
    Console.WriteLine("\n🔍 Analyzing local network...");
    Console.WriteLine("This may take a few moments depending on your network size.");

    var options = AnalysisOptions.Default;
    var result = await NetworkAnalyzerEngine.AnalyzeNetworkAsync(options);

    return result.Match(
        Right: analysisResult =>
        {
            GlobalVariables.lastAnalysisResult = Some(analysisResult);
            Console.Clear();
            var report = ReportGenerator.GenerateReport(analysisResult, ReportFormat.Console);
            Console.WriteLine(report);
            return Some(-1);
        },
        Left: error =>
        {
            Console.WriteLine($"❌ Error: {error.Message}");
            return None;
        }
    );
}

static async Task<Option<int>> AnalyzeSpecificNetwork()
{
    Console.Write("\nEnter network range (CIDR format, e.g., 192.168.1.0/24): ");
    var network = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(network))
    {
        Console.WriteLine("❌ Invalid network range.");
        return None;
    }

    Console.WriteLine($"\n🔍 Analyzing network {network}...");

    var options = AnalysisOptions.Default with { TargetNetwork = Some(network) };
    var result = await NetworkAnalyzerEngine.AnalyzeNetworkAsync(options);

    return result.Match(
        Right: analysisResult =>
        {
            GlobalVariables.lastAnalysisResult = Some(analysisResult);
            Console.Clear();
            var report = ReportGenerator.GenerateReport(analysisResult, ReportFormat.Console);
            Console.WriteLine(report);
            return Some(-1);
        },
        Left: error =>
        {
            Console.WriteLine($"❌ Error: {error.Message}");
            return None;
        }
    );
}

static async Task<Option<int>> PingSingleDevice()
{
    Console.Write("\nEnter IP address or hostname: ");
    var target = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(target))
    {
        Console.WriteLine("❌ Invalid target.");
        return None;
    }

    if (!IPAddress.TryParse(target, out var ipAddress))
    {
        try
        {
            var hostEntry = await Dns.GetHostEntryAsync(target);
            ipAddress = hostEntry.AddressList.FirstOrDefault(addr =>
                addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (ipAddress == null)
            {
                Console.WriteLine("❌ Could not resolve hostname.");
                return None;
            }
        }
        catch
        {
            Console.WriteLine("❌ Could not resolve hostname.");
            return None;
        }
    }

    Console.WriteLine($"\n🏓 Pinging {target} ({ipAddress})...");

    var result = await NetworkAnalyzerEngine.AnalyzeSingleDeviceAsync(ipAddress);

    return result.Match(
        Right: device =>
        {
            Console.WriteLine();
            var report = ReportGenerator.GenerateDeviceSummary(device, ReportFormat.Console);
            Console.WriteLine(report);
            return Some(-1);
        },
        Left: error =>
        {
            Console.WriteLine($"❌ Error: {error.Message}");
            return None;
        }
    );
}

static async Task<Option<int>> ContinuousPing()
{
    Console.Write("\nEnter IP address or hostname: ");
    var target = Console.ReadLine()?.Trim();

    Console.Write("Enter number of pings (default: 4): ");
    var countInput = Console.ReadLine()?.Trim();
    var count = int.TryParse(countInput, out var c) ? c : 4;

    if (string.IsNullOrEmpty(target))
    {
        Console.WriteLine("❌ Invalid target.");
        return None;
    }

    if (!IPAddress.TryParse(target, out var ipAddress))
    {
        try
        {
            var hostEntry = await Dns.GetHostEntryAsync(target);
            ipAddress = hostEntry.AddressList.FirstOrDefault(addr =>
                addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (ipAddress == null)
            {
                Console.WriteLine("❌ Could not resolve hostname.");
                return None;
            }
        }
        catch
        {
            Console.WriteLine("❌ Could not resolve hostname.");
            return None;
        }
    }

    Console.WriteLine($"\n🏓 Continuous ping to {target} ({ipAddress}) - {count} packets...");

    var result = await NetworkAnalyzerEngine.ContinuousPingAsync(ipAddress, count);

    return result.Match(
        Right: results =>
        {
            Console.WriteLine();
            Console.WriteLine("═══ Ping Results ═══");

            var successful = 0;
            var totalTime = 0L;

            foreach (var (pingResult, index) in results.Select((r, i) => (r, i + 1)))
            {
                var status = pingResult.success ? "✓" : "✗";
                var time = pingResult.success ? $"{pingResult.ResponseTime}ms" : "Failed";

                Console.WriteLine($"{index:D2}. {status} {pingResult.Target} - {time} ({pingResult.Status})");

                if (pingResult.success)
                {
                    successful++;
                    totalTime += pingResult.ResponseTime;
                }
            }

            Console.WriteLine();
            Console.WriteLine("═══ Statistics ═══");
            Console.WriteLine($"Packets: Sent = {count}, Received = {successful}, Lost = {count - successful} ({(double)(count - successful) / count * 100:F1}% loss)");

            if (successful > 0)
            {
                var avgTime = totalTime / successful;
                Console.WriteLine($"Approximate round trip times: Average = {avgTime}ms");
            }

            return Some(-1);
        },
        Left: error =>
        {
            Console.WriteLine($"❌ Error: {error.Message}");
            return None;
        }
    );
}

static async Task<Option<int>> TraceRoute()
{
    Console.Write("\nEnter IP address or hostname: ");
    var target = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(target))
    {
        Console.WriteLine("❌ Invalid target.");
        return None;
    }

    if (!IPAddress.TryParse(target, out var ipAddress))
    {
        try
        {
            var hostEntry = await Dns.GetHostEntryAsync(target);
            ipAddress = hostEntry.AddressList.FirstOrDefault(addr =>
                addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (ipAddress == null)
            {
                Console.WriteLine("❌ Could not resolve hostname.");
                return None;
            }
        }
        catch
        {
            Console.WriteLine("❌ Could not resolve hostname.");
            return None;
        }
    }

    Console.WriteLine($"\n🛣️  Tracing route to {target} ({ipAddress})...");

    var result = await PingService.TraceRouteAsync(ipAddress);

    return result.Match(
        Right: hops =>
        {
            Console.WriteLine();
            Console.WriteLine("═══ Trace Route Results ═══");

            if (hops.IsEmpty)
            {
                Console.WriteLine("No route found to target.");
            }
            else
            {
                foreach (var (hop, index) in hops.Select((h, i) => (h, i + 1)))
                {
                    Console.WriteLine($"{index:D2}. {hop}");
                }
            }

            return Some(-1);
        },
        Left: error =>
        {
            Console.WriteLine($"❌ Error: {error.Message}");
            return None;
        }
    );
}

static async Task<Option<int>> ExportResults()
{
    if (GlobalVariables.lastAnalysisResult.IsNone)
    {
        Console.WriteLine("\n📤 No analysis results available to export.");
        Console.WriteLine("Please run a network analysis first (options 1 or 2).");
        return None;
    }

    var analysisResult = GlobalVariables.lastAnalysisResult.Value();

    Console.WriteLine("\n📤 Export Analysis Results");
    Console.WriteLine("Available formats:");
    Console.WriteLine("1. JSON");
    Console.WriteLine("2. CSV");
    Console.WriteLine("3. Markdown");
    Console.Write("Select format (1-3): ");
    var formatChoice = Console.ReadLine()?.Trim();
    var format = formatChoice switch
    {
        "1" => ReportFormat.Json,
        "2" => ReportFormat.Csv,
        "3" => ReportFormat.Markdown,
        _ => ReportFormat.Json
    };
    var extension = format switch
    {
        ReportFormat.Json => "json",
        ReportFormat.Csv => "csv",
        ReportFormat.Markdown => "md",
        _ => "json"
    };
    var defaultFilename = $"network-analysis-{DateTime.Now:yyyy-MM-dd-HHmmss}.{extension}";
    Console.Write($"Enter filename (default: {defaultFilename}): ");
    var filename = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(filename))
    {
        filename = defaultFilename;
    }
    try
    {
        var report = ReportGenerator.GenerateReport(analysisResult, format);
        await File.WriteAllTextAsync(filename, report);
        Console.WriteLine($"✅ Results exported successfully to: {filename}");
        Console.WriteLine($"📁 Full path: {Path.GetFullPath(filename)}");
        return Some(-1);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Failed to export results: {ex.Message}");
        return None;
    }
}

static async Task<int> ScanNetwork(string network)
{
    Console.WriteLine($"Scanning network: {network}");

    var options = AnalysisOptions.Default with { TargetNetwork = Some(network) };
    var result = await NetworkAnalyzerEngine.AnalyzeNetworkAsync(options);

    return result.Match(
        Right: analysisResult =>
        {
            GlobalVariables.lastAnalysisResult = Some(analysisResult);
            var report = ReportGenerator.GenerateReport(analysisResult, ReportFormat.Console);
            Console.WriteLine(report);
            return -1;
        },
        Left: error =>
        {
            Console.WriteLine($"Error: {error.Message}");
            return 1;
        }
    );
}

static async Task<int> PingDevice(string target)
{
    if (!IPAddress.TryParse(target, out var ipAddress))
    {
        try
        {
            var hostEntry = await Dns.GetHostEntryAsync(target);
            ipAddress = hostEntry.AddressList.FirstOrDefault(addr =>
                addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (ipAddress == null)
            {
                Console.WriteLine("Could not resolve hostname.");
                return 1;
            }
        }
        catch
        {
            Console.WriteLine("Could not resolve hostname.");
            return 1;
        }
    }

    var result = await NetworkAnalyzerEngine.AnalyzeSingleDeviceAsync(ipAddress);

    return result.Match(
        Right: device =>
        {
            var report = ReportGenerator.GenerateDeviceSummary(device, ReportFormat.Console);
            Console.WriteLine(report);
            return 0;
        },
        Left: error =>
        {
            Console.WriteLine($"Error: {error.Message}");
            return 1;
        }
    );
}

static async Task<int> TraceRouteDevice(string target)
{
    if (!IPAddress.TryParse(target, out var ipAddress))
    {
        try
        {
            var hostEntry = await Dns.GetHostEntryAsync(target);
            ipAddress = hostEntry.AddressList.FirstOrDefault(addr =>
                addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (ipAddress == null)
            {
                Console.WriteLine("Could not resolve hostname.");
                return 1;
            }
        }
        catch
        {
            Console.WriteLine("Could not resolve hostname.");
            return 1;
        }
    }

    var result = await PingService.TraceRouteAsync(ipAddress);

    return result.Match(
        Right: hops =>
        {
            Console.WriteLine($"Trace route to {target} ({ipAddress}):");

            if (hops.IsEmpty)
            {
                Console.WriteLine("No route found to target.");
            }
            else
            {
                foreach (var (hop, index) in hops.Select((h, i) => (h, i + 1)))
                {
                    Console.WriteLine($"{index:D2}. {hop}");
                }
            }

            return 0;
        },
        Left: error =>
        {
            Console.WriteLine($"Error: {error.Message}");
            return 1;
        }
    );
}

static async Task<int> AnalyzeLocalNetworkCommand()
{
    Console.WriteLine("Analyzing local network...");

    var options = AnalysisOptions.Default;
    var result = await NetworkAnalyzerEngine.AnalyzeNetworkAsync(options);

    return result.Match(
        Right: analysisResult =>
        {
            GlobalVariables.lastAnalysisResult = Some(analysisResult);
            var report = ReportGenerator.GenerateReport(analysisResult, ReportFormat.Console);
            Console.WriteLine(report);
            return 0;
        },
        Left: error =>
        {
            Console.WriteLine($"Error: {error.Message}");
            return 1;
        }
    );
}

static int ShowUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  NetworkAnalyzer                    - Interactive mode");
    Console.WriteLine("  NetworkAnalyzer scan <network>     - Scan specific network (CIDR)");
    Console.WriteLine("  NetworkAnalyzer ping <target>      - Ping single device");
    Console.WriteLine("  NetworkAnalyzer trace <target>     - Trace route to target");
    Console.WriteLine("  NetworkAnalyzer local              - Analyze local network");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  NetworkAnalyzer scan 192.168.1.0/24");
    Console.WriteLine("  NetworkAnalyzer ping google.com");
    Console.WriteLine("  NetworkAnalyzer trace 8.8.8.8");

    return 1;
}