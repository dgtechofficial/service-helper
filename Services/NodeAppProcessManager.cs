
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PostgresRedisManager.Models;

namespace PostgresRedisManager.Services;

internal sealed class AppProcessEventArgs(
    Guid appId,
    string appName,
    string message) : EventArgs
{
    public Guid AppId { get; } = appId;
    public string AppName { get; } = appName;
    public string Message { get; } = message;
}

internal sealed class AppProcessManager : IDisposable
{
    private sealed record RunningApp(
        Process Process,
        ChildProcessJob? Job);

    private readonly ConcurrentDictionary<Guid, RunningApp> _processes = new();
    private readonly object _sync = new();

    private bool _disposed;

    public event EventHandler<AppProcessEventArgs>? OutputReceived;
    public event EventHandler<AppProcessEventArgs>? StateChanged;

    public bool IsRunning(Guid appId)
    {
        return _processes.TryGetValue(appId, out var running)
            && IsAlive(running.Process);
    }

    // Supports applications that require a specific project file.
    public void Start(
        AppProcessConfig app,
        string projectFile,
        string projectType)
    {
        ArgumentNullException.ThrowIfNull(app);

        StartCore(
            app.Id,
            app.Name,
            app.Directory,
            app.RunCommand,
            projectFile,
            projectType);
    }

    private void StartCore(
        Guid appId,
        string appName,
        string directory,
        string runCommand,
        string? projectFile,
        string? projectType)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (string.IsNullOrWhiteSpace(directory)
                || !Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException(
                    $"The configured project directory does not exist: {directory}");
            }

            if (!string.IsNullOrWhiteSpace(projectFile)
                && !File.Exists(Path.Combine(directory, projectFile)))
            {
                throw new FileNotFoundException(
                    $"No {projectFile} file was found in the configured "
                    + $"{projectType} project directory.",
                    Path.Combine(directory, projectFile));
            }

            if (string.IsNullOrWhiteSpace(runCommand))
            {
                throw new InvalidOperationException(
                    $"No run command is configured for {appName}.");
            }

            if (_processes.TryGetValue(appId, out var existing))
            {
                if (IsAlive(existing.Process))
                {
                    throw new InvalidOperationException(
                        $"{appName} is already running.");
                }

                if (_processes.TryRemove(appId, out var stale))
                {
                    stale.Job?.Dispose();
                    stale.Process.Dispose();
                }
            }

            var commandProcessor =
                Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";

            var startInfo = new ProcessStartInfo
            {
                FileName = commandProcessor,
                WorkingDirectory = directory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/s");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(runCommand);

            ChildProcessJob? job = TryCreateJob();

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = false
            };

            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    OutputReceived?.Invoke(
                        this,
                        new AppProcessEventArgs(
                            appId, appName, args.Data));
                }
            };

            process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    OutputReceived?.Invoke(
                        this,
                        new AppProcessEventArgs(
                            appId, appName, args.Data));
                }
            };

            process.Exited += (_, _) =>
            {
                int exitCode;

                try
                {
                    exitCode = process.ExitCode;
                }
                catch (InvalidOperationException)
                {
                    exitCode = -1;
                }

                // Remove only this process instance, never a newer one.
                if (_processes.TryGetValue(appId, out var current)
                    && ReferenceEquals(current.Process, process))
                {
                    ((ICollection<KeyValuePair<Guid, RunningApp>>)_processes)
                        .Remove(new KeyValuePair<Guid, RunningApp>(
                            appId, current));
                }

                try
                {
                    job?.Dispose();
                }
                finally
                {
                    process.Dispose();
                }

                StateChanged?.Invoke(
                    this,
                    new AppProcessEventArgs(
                        appId,
                        appName,
                        $"Stopped (exit code {exitCode})"));
            };

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException(
                        "Windows could not start the command processor.");
                }

                if (job is not null)
                {
                    try
                    {
                        job.Add(process);
                    }
                    catch (Exception exception)
                        when (exception is Win32Exception
                            or InvalidOperationException)
                    {
                        job.Dispose();
                        job = null;
                    }
                }

                var running = new RunningApp(process, job);

                if (!_processes.TryAdd(appId, running))
                {
                    Terminate(running);

                    throw new InvalidOperationException(
                        $"{appName} is already starting or running.");
                }

                // Register first, then enable events. If the process has
                // already exited, the Exited event can now clean it up.
                process.EnableRaisingEvents = true;

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                StateChanged?.Invoke(
                    this,
                    new AppProcessEventArgs(
                        appId, appName, $"Running: {runCommand}"));
            }
            catch
            {
                if (_processes.TryGetValue(appId, out var current)
                    && ReferenceEquals(current.Process, process))
                {
                    ((ICollection<KeyValuePair<Guid, RunningApp>>)_processes)
                        .Remove(new KeyValuePair<Guid, RunningApp>(
                            appId, current));
                }

                try
                {
                    Terminate(new RunningApp(process, job));
                }
                finally
                {
                    process.Dispose();
                }

                throw;
            }
        }
    }

    public async Task StopAsync(Guid appId)
    {
        RunningApp? running;

        lock (_sync)
        {
            if (_disposed
                || !_processes.TryGetValue(appId, out running))
            {
                return;
            }
        }

        if (!IsAlive(running.Process))
            return;

        Terminate(running);

        try
        {
            await running.Process
                .WaitForExitAsync()
                .WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (Exception exception)
            when (exception is InvalidOperationException
                or Win32Exception
                or TimeoutException
                or ObjectDisposedException)
        {
            // The process may already have exited or been disposed.
        }
    }

    public async Task StopAllAsync()
    {
        Guid[] appIds = _processes.Keys.ToArray();

        await Task.WhenAll(appIds.Select(StopAsync));
    }

    public void Dispose()
    {
        RunningApp[] runningApps;

        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;

            runningApps = _processes.Values.ToArray();
            _processes.Clear();
        }

        foreach (var running in runningApps)
        {
            Terminate(running);

            try
            {
                running.Process.Dispose();
            }
            catch (InvalidOperationException)
            {
                // Process cleanup is best-effort during disposal.
            }
        }

        OutputReceived = null;
        StateChanged = null;
    }

    private static void Terminate(RunningApp running)
    {
        try
        {
            if (IsAlive(running.Process))
                running.Process.Kill(entireProcessTree: true);
        }
        catch (Exception exception)
            when (exception is InvalidOperationException
                or Win32Exception
                or NotSupportedException
                or ObjectDisposedException)
        {
            // The process may have exited during termination.
        }

        try
        {
            running.Job?.Dispose();
        }
        catch (Win32Exception)
        {
            // Best-effort cleanup.
        }
    }

    private static ChildProcessJob? TryCreateJob()
    {
        try
        {
            return new ChildProcessJob();
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    private static bool IsAlive(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
