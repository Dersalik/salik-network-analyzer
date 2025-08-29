# NetworkAnalyzer

A .NET 8 console application for network analysis and monitoring, built with functional programming using LanguageExt.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![C#](https://img.shields.io/badge/C%23-12.0-239120?style=flat-square&logo=c-sharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![LanguageExt](https://img.shields.io/badge/LanguageExt-4.4.9-4CAF50?style=flat-square)](https://github.com/louthy/language-ext)
[![License](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](LICENSE)

## Quick Start

```bash
git clone https://github.com/yourusername/NetworkAnalyzer.git
cd NetworkAnalyzer
dotnet run --project NetworkAnalyzer -- local
```

## Features

- **Network Discovery** - Scan local networks or CIDR ranges
- **Device Analysis** - Ping devices with detailed statistics
- **Continuous Monitoring** - Monitor availability over time
- **Trace Route** - Track network paths
- **Multiple Formats** - Console, JSON, CSV, Markdown output
- **Functional Design** - Immutable data, Option/Either types, error handling

## Usage

### Command Line
```bash
dotnet run --project NetworkAnalyzer -- scan 192.168.1.0/24
dotnet run --project NetworkAnalyzer -- ping google.com
dotnet run --project NetworkAnalyzer -- trace 8.8.8.8
dotnet run --project NetworkAnalyzer                    # Interactive mode
```

### Programming
```csharp
using NetworkAnalyzer;
using static LanguageExt.Prelude;

// Analyze network with error handling
var result = await NetworkAnalyzerEngine.AnalyzeNetworkAsync(AnalysisOptions.Default);

var report = result.Match(
    Right: analysisResult => ReportGenerator.GenerateReport(analysisResult, ReportFormat.Console),
    Left: error => $"Error: {error.Message}"
);

// Functional composition
var devices = await NetworkRange.FromCidr("192.168.1.0/24")
    .ToAsync()
    .Bind(range => NetworkDiscovery.DiscoverDevicesAsync(range));
```

## Architecture

Built with functional programming principles:

- **Immutable Records** - All data structures are immutable
- **Option Types** - Safe nullable handling with `Option<T>`
- **Either Types** - Explicit error handling with `Either<Error, Success>`
- **Monadic Composition** - Chainable operations with automatic error propagation

### Core APIs
```csharp
NetworkAnalyzerEngine.AnalyzeNetworkAsync(options)      // Full network analysis
NetworkAnalyzerEngine.AnalyzeSingleDeviceAsync(ip)     // Single device analysis
NetworkAnalyzerEngine.ContinuousPingAsync(ip, count)   // Continuous monitoring
NetworkDiscovery.DiscoverDevicesAsync(range)           // Device discovery
PingService.PingAsync(target, config)                  // Low-level ping
```

## Configuration

```csharp
var options = new AnalysisOptions(
    PingConfig: new PingConfiguration(Timeout: 2000, BufferSize: 32, Ttl: 64, DontFragment: true),
    MaxConcurrency: 50,
    IncludeTraceRoute: false,
    TargetNetwork: Some("192.168.1.0/24")
);
```

## Testing

```bash
dotnet test                                    # Run all tests
dotnet test --collect:"XPlat Code Coverage"   # With coverage
```

## Contributing

1. Fork the repo
2. Create feature branch
3. Add tests
4. Submit pull request

## License

MIT License - see [LICENSE](LICENSE) file.

## Links

- [Issues](https://github.com/yourusername/NetworkAnalyzer/issues)
- [Discussions](https://github.com/yourusername/NetworkAnalyzer/discussions)
- [Wiki](https://github.com/yourusername/NetworkAnalyzer/wiki)
