namespace Benchmarks;

public class Program
{
    public static async Task Main(string[] args)
    {
        await DisassemblyDiagnoserTests.RunDumpThisProcessAsync(CancellationToken.None).ConfigureAwait(false);
        // await DisassemblyDiagnoserTests.RunDumpThisProcessWithDotNetDumpAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
