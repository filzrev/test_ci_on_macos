namespace Benchmarks;

public class Program
{
    public static async Task Main(string[] args)
    {
        await DisassemblyDiagnoserTests.RunDumpThisProcessWithDotNetDumpAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
