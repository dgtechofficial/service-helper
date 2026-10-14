using System.ComponentModel;
using System.ServiceProcess;

namespace PostgresRedisManager;

internal static class RedisWindowsServiceManager
{
    public static ServiceControllerStatus GetStatus(string serviceName)
    {
        using var service = OpenService(serviceName);
        service.Refresh();
        return service.Status;
    }

    public static Task ControlAsync(string serviceName, string action)
    {
        return Task.Run(() =>
        {
            using var service = OpenService(serviceName);
            try
            {
                service.Refresh();
                switch (action)
                {
                    case "start":
                        if (service.Status == ServiceControllerStatus.Running)
                            return;
                        if (service.Status == ServiceControllerStatus.StopPending)
                            service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        break;

                    case "stop":
                        if (service.Status == ServiceControllerStatus.Stopped)
                            return;
                        if (!service.CanStop)
                            throw new InvalidOperationException("Windows reports that this service cannot be stopped.");
                        if (service.Status == ServiceControllerStatus.StartPending)
                            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(action));
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or ArgumentException)
            {
                throw ExplainFailure(serviceName, exception);
            }
        });
    }

    private static ServiceController OpenService(string serviceName)
    {
        var service = new ServiceController(serviceName);
        try
        {
            _ = service.Status;
            return service;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or ArgumentException)
        {
            service.Dispose();
            throw ExplainFailure(serviceName, exception);
        }
    }

    private static InvalidOperationException ExplainFailure(string serviceName, Exception exception)
    {
        var win32Exception = FindWin32Exception(exception);
        if (win32Exception?.NativeErrorCode == 5)
        {
            return new InvalidOperationException(
                $"Access was denied while opening or controlling the Redis Windows service '{serviceName}'. Restart this manager using Run as administrator. Windows reported: {win32Exception.Message}",
                exception);
        }

        if (win32Exception?.NativeErrorCode == 1060)
        {
            return new InvalidOperationException(
                $"The Redis Windows service '{serviceName}' is not installed. Verify the service name in Redis Settings.",
                exception);
        }

        return new InvalidOperationException(
            $"Windows could not open or control the Redis service '{serviceName}'. {exception.Message}",
            exception);
    }

    private static Win32Exception? FindWin32Exception(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is Win32Exception win32Exception)
                return win32Exception;
        }

        return null;
    }
}
