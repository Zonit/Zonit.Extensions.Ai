using BenchmarkDotNet.Running;

namespace Zonit.Extensions.Ai.Benchmarks;

// Run all benchmarks:        dotnet run -c Release
// Run one class:             dotnet run -c Release -- --filter *SchemaBenchmarks*
// List available:            dotnet run -c Release -- --list flat
internal static class Program
{
    private static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
