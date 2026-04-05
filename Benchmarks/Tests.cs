using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Disassemblers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace Benchmarks;

public class DisassemblyDiagnoserTests
{
    protected ITestOutputHelper Output => TestContext.Current.TestOutputHelper!;

    private static void AppendLogLine(string message)
        => File.AppendAllText("log_temp.diag", message + Environment.NewLine);

    [Fact]
    public async Task CanDisassemble()
    {
        // Arrange
        var jit = Jit.RyuJit;
        var platform = Platform.Arm64;
        IToolchain toolchain = InProcessEmitToolchain.Default;

        var disassemblyDiagnoser = new DisassemblyDiagnoser(
            new DisassemblyDiagnoserConfig(printSource: false, maxDepth: 3));

        var config = CreateConfig(jit, platform, toolchain, disassemblyDiagnoser, RunStrategy.ColdStart);

        // Act
        AppendLogLine("Before::RunAsync");
        var summary = await BenchmarkRunner.RunAsync<WithCalls>(GetMinimalConfig(config), cancellationToken: TestContext.Current.CancellationToken);
        AppendLogLine(" After::RunAsync");

        // Assert
        ValidateSummary(summary);
        DisassemblyResult result = disassemblyDiagnoser.Results.Single().Value;
        Assert.Empty(result.Errors);
    }

    public class WithCalls
    {
        [Benchmark]
        [Arguments(int.MaxValue)]
        public void Benchmark(int someArgument)
        {
            if (someArgument != int.MaxValue)
                throw new InvalidOperationException("Wrong value of the argument!!");

            // we should rather have test per use case
            // but running so many tests for all JITs would take too much time
            // so we have one method that does it all
            Static();
            Instance();
            Recursive();

            Benchmark(true);
        }

        [MethodImpl(MethodImplOptions.NoInlining)] public static void Static() { }

        [MethodImpl(MethodImplOptions.NoInlining)] public void Instance() { }

        [MethodImpl(MethodImplOptions.NoInlining)] // legacy JIT x64 was able to inline this method ;)
        public void Recursive()
        {
            if (new Random(123).Next(0, 10) == 11) // never true, but JIT does not know it
                Recursive();
        }

        [MethodImpl(MethodImplOptions.NoInlining)] public void Benchmark(bool justAnOverload) { } // we need to test overloads (#562)
    }

    private static void ValidateSummary(Summary summary)
    {
        Assert.False(summary.HasCriticalValidationErrors, "The \"Summary\" should have NOT \"HasCriticalValidationErrors\"");

        Assert.True(summary.Reports.Any(), "The \"Summary\" should contain at least one \"BenchmarkReport\" in the \"Reports\" collection");

        //summary.CheckPlatformLinkerIssues();

        Assert.True(summary.Reports.All(r => r.BuildResult.IsBuildSuccess),
            "The following benchmarks have failed to build: " +
            string.Join(", ", summary.Reports.Where(r => !r.BuildResult.IsBuildSuccess).Select(r => r.BenchmarkCase.DisplayInfo)));

        Assert.True(summary.Reports.All(r => r.ExecuteResults != null),
            "The following benchmarks don't have any execution results: " +
            string.Join(", ", summary.Reports.Where(r => r.ExecuteResults == null).Select(r => r.BenchmarkCase.DisplayInfo)));

        Assert.True(summary.Reports.All(r => r.ExecuteResults.All(er => er.IsSuccess)),
            "All reports should have succeeded to execute");
    }

    protected IConfig CreateSimpleConfig(OutputLogger? logger = null, Job? job = null)
    {
        var baseConfig = job == null ? (IConfig)new SingleRunFastConfig() : new SingleJobConfig(job);
        return baseConfig
            .AddLogger(logger ?? (Output != null ? new OutputLogger(Output) : ConsoleLogger.Default))
            .AddColumnProvider(DefaultColumnProviders.Instance)
            .AddAnalyser(DefaultConfig.Instance.GetAnalysers().ToArray());
    }

    private IConfig CreateConfig(Jit jit, Platform platform, IToolchain toolchain, IDiagnoser disassemblyDiagnoser, RunStrategy runStrategy)
          => ManualConfig.CreateEmpty()
              .AddJob(Job.Dry.WithJit(jit)
                  .WithPlatform(platform)
                  .WithToolchain(toolchain)
                  .WithStrategy(runStrategy)
                  // Ensure the build goes through the full process and doesn't build without dependencies like most of the integration tests do.
#if RELEASE
                  .WithCustomBuildConfiguration("Release")
#else
                    .WithCustomBuildConfiguration("Debug")
#endif
              )
              .AddLogger(DefaultConfig.Instance.GetLoggers().ToArray())
              .AddColumnProvider(DefaultColumnProviders.Instance)
              .AddDiagnoser(disassemblyDiagnoser)
              .AddLogger(new OutputLogger(Output));

    private IConfig GetMinimalConfig(IConfig? config)
    {
        // Add logging, so the Benchmark execution is in the TestRunner output (makes Debugging easier)
        if (config == null)
            config = CreateSimpleConfig();

        if (!config.GetLoggers().OfType<OutputLogger>().Any())
            config = config.AddLogger(Output != null ? new OutputLogger(Output) : ConsoleLogger.Default);
        if (!config.GetLoggers().OfType<ConsoleLogger>().Any())
            config = config.AddLogger(ConsoleLogger.Default);

        if (!config.GetColumnProviders().Any())
            config = config.AddColumnProvider(DefaultColumnProviders.Instance);

        return config!;
    }

}

public class SingleJobConfig : ManualConfig
{
    public SingleJobConfig(Job job)
    {
        AddJob(job);
    }
}

public class SingleRunFastConfig : ManualConfig
{
    public SingleRunFastConfig()
    {
        AddJob(Job.Dry);
    }
}