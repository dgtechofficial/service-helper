using System.Diagnostics;
using PostgresRedisManager.Models;

namespace PostgresRedisManager.Services;

internal sealed record ProcessResult(int ExitCode, string Output);

internal static class PostgreSqlProcessManager
{
    public static Task<ProcessResult> StartAsync(PostgreSqlConfig config) => RunAsync(config, "start");

    public static Task<ProcessResult> StopAsync(PostgreSqlConfig config) => RunAsync(config, "stop");

    public static Task<ProcessResult> GetStatusAsync(PostgreSqlConfig config) => RunAsync(config, "status");

    private static async Task<ProcessResult> RunAsync(PostgreSqlConfig config, string action)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = config.PgCtlPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        var executableDirectory = Path.GetDirectoryName(config.PgCtlPath);
        if (!string.IsNullOrWhiteSpace(executableDirectory))
            startInfo.WorkingDirectory = executableDirectory;

        startInfo.ArgumentList.Add("-D");
        startInfo.ArgumentList.Add(config.DataDirectory);
        if (action == "start")
        {
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add($"-p {config.Port}");
            startInfo.ArgumentList.Add("-w");
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add("30");
            startInfo.ArgumentList.Add("start");
        }
        else if (action == "stop")
        {
            startInfo.ArgumentList.Add("-m");
            startInfo.ArgumentList.Add("fast");
            startInfo.ArgumentList.Add("-w");
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add("30");
            startInfo.ArgumentList.Add("stop");
        }
        else
        {
            startInfo.ArgumentList.Add("status");
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch { }
            throw new TimeoutException("The PostgreSQL command timed out after 40 seconds.");
        }

        var output = (await stdout) + Environment.NewLine + (await stderr);
        return new ProcessResult(process.ExitCode, output.Trim());
    }
}
