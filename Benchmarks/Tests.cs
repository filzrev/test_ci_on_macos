using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Runtime;
using System.Diagnostics;

namespace Benchmarks;

public class DisassemblyDiagnoserTests
{
    // Helper method to output log messages to file. (When running with MTP mode. Stdout is captured and not outputted before test finished)
    private static void AppendLogLine(string message)
        => File.AppendAllText("log_temp.diag", message + Environment.NewLine);

    internal static async Task<DataTarget> RunDumpThisProcessAsync(CancellationToken cancellationToken)
    {
        var processId = Process.GetCurrentProcess().Id;

        string dumpPath = Path.GetTempFileName() + ".dmp";

        try
        {
            var client = new DiagnosticsClient(processId);

            try
            {
                var flags = WriteDumpFlags.LoggingEnabled | WriteDumpFlags.VerboseLoggingEnabled;

                AppendLogLine("[START] WriteDumpAsync");
                await client.WriteDumpAsync(DumpType.Full, dumpPath, flags, cancellationToken).ConfigureAwait(false);
                AppendLogLine("[  END] WriteDumpAsync");
            }
            catch (ServerErrorException sxe)
            {
                throw new ArgumentException($"Unable to create a snapshot of process {processId:x}.", sxe);
            }
            return DataTarget.LoadDump(dumpPath);
        }
        finally
        {
            // TODO: It can't delete dump file before DataTarget is disposed.
            // File.Delete(dumpPath);
        }
    }

    internal static async Task<DataTarget> RunDumpThisProcessWithDotNetDumpAsync(CancellationToken cancellationToken)
    {
        var processId = Process.GetCurrentProcess().Id;

        string dumpPath = Path.GetTempFileName() + ".dmp";
        try
        {
            await Process.Start("dotnet", "tool install dotnet-dump --local --create-manifest-if-needed").WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            AppendLogLine("[START] dotnet-dump");
            await Process.Start("dotnet", $"tool run dotnet-dump collect --process-id {processId} --type Full --output {dumpPath} --diag").WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            AppendLogLine("[  End] dotnet-dump");

            return DataTarget.LoadDump(dumpPath);
        }
        finally
        {
            // TODO: It can't delete dump file before DataTarget is disposed.
            //File.Delete(dumpPath);
        }
    }
}
