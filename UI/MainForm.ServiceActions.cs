using System.ServiceProcess;
using PostgresRedisManager.Services;

namespace PostgresRedisManager.UI;

internal sealed partial class MainForm
{
    private async Task StartBothAsync()
    {
        if (_bulkOperating) return;
        SetBulkBusy(true);
        try
        {
            await ControlRedisAsync("start", askConfirmation: false, showNotifications: false);
            await StartPostgresAsync(showNotifications: false);
            await RefreshAllAsync();
            MessageBox.Show("Start requests were processed. Review each service status above.",
                "Services Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            SetBulkBusy(false);
        }
    }

    private async Task StopBothAsync()
    {
        if (_bulkOperating) return;
        var answer = MessageBox.Show(
            "Stop Redis and PostgreSQL? Applications using either service may lose their connection.",
            "Confirm stop all", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes) return;

        SetBulkBusy(true);
        try
        {
            await ControlRedisAsync("stop", askConfirmation: false, showNotifications: false);
            await StopPostgresAsync(askConfirmation: false, showNotifications: false);
            await RefreshAllAsync();
            MessageBox.Show("Stop requests were processed. Review each service status above.",
                "Services Manager", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            SetBulkBusy(false);
        }
    }

    private async Task StartPostgresAsync(bool showNotifications = true)
    {
        if (await TcpPortProbe.IsOpenAsync(_config.PostgreSQL.Host, _config.PostgreSQL.Port, TimeSpan.FromSeconds(1)))
        {
            if (showNotifications)
                MessageBox.Show("PostgreSQL is already accepting TCP connections on the configured endpoint.",
                    "PostgreSQL already running", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshPanelAsync(_postgresPanel);
            return;
        }

        if (!File.Exists(_config.PostgreSQL.PgCtlPath))
        {
            ShowError($"pg_ctl.exe was not found:\n{_config.PostgreSQL.PgCtlPath}\n\nUpdate the path in PostgreSQL Settings.");
            return;
        }
        if (!Directory.Exists(_config.PostgreSQL.DataDirectory))
        {
            ShowError($"PostgreSQL data directory was not found:\n{_config.PostgreSQL.DataDirectory}");
            return;
        }

        var clusterStatus = await PostgreSqlProcessManager.GetStatusAsync(_config.PostgreSQL);
        if (clusterStatus.ExitCode == 0)
        {
            if (showNotifications)
                MessageBox.Show("The configured PostgreSQL cluster is already running.",
                    "PostgreSQL already running", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshPanelAsync(_postgresPanel);
            return;
        }

        _postgresPanel.SetBusy(true);
        try
        {
            var result = await PostgreSqlProcessManager.StartAsync(_config.PostgreSQL);
            if (result.ExitCode != 0)
                ShowError("PostgreSQL did not start.\n\n" + result.Output);
            else
            {
                await RefreshPanelCoreAsync(
                    _postgresPanel,
                    _config.PostgreSQL.Host,
                    _config.PostgreSQL.Port);

                if (showNotifications)
                    MessageBox.Show("PostgreSQL start command completed.\n\n" + result.Output,
                        "PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            _postgresPanel.SetBusy(false);
            await RefreshPanelAsync(_postgresPanel);
        }
    }

    private async Task StopPostgresAsync(bool askConfirmation = true, bool showNotifications = true)
    {
        if (!File.Exists(_config.PostgreSQL.PgCtlPath))
        {
            ShowError($"pg_ctl.exe was not found:\n{_config.PostgreSQL.PgCtlPath}");
            return;
        }

        if (!Directory.Exists(_config.PostgreSQL.DataDirectory))
        {
            ShowError($"PostgreSQL data directory was not found:\n{_config.PostgreSQL.DataDirectory}");
            return;
        }

        var clusterStatus = await PostgreSqlProcessManager.GetStatusAsync(_config.PostgreSQL);
        if (clusterStatus.ExitCode != 0)
        {
            if (showNotifications)
                MessageBox.Show("The configured PostgreSQL cluster is already stopped.",
                    "PostgreSQL appears stopped", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshPanelAsync(_postgresPanel);
            return;
        }

        if (askConfirmation)
        {
            var answer = MessageBox.Show(
                "Stop PostgreSQL gracefully? Applications using the database may lose their connection.",
                "Confirm PostgreSQL stop", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;
        }

        _postgresPanel.SetBusy(true);
        try
        {
            var result = await PostgreSqlProcessManager.StopAsync(_config.PostgreSQL);
            if (result.ExitCode != 0)
                ShowError("PostgreSQL did not stop cleanly.\n\n" + result.Output);
            else if (showNotifications)
                MessageBox.Show("PostgreSQL stopped.\n\n" + result.Output,
                    "PostgreSQL", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            _postgresPanel.SetBusy(false);
            await RefreshPanelAsync(_postgresPanel);
        }
    }

    private async Task ControlRedisAsync(string action, bool askConfirmation = true, bool showNotifications = true)
    {
        if (action == "stop" && askConfirmation)
        {
            var answer = MessageBox.Show(
                "Stop the Redis Windows service? Applications using Redis may lose their connection.",
                "Confirm Redis stop", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;
        }

        _redisPanel.SetBusy(true);
        try
        {
            var status = RedisWindowsServiceManager.GetStatus(_config.Redis.ServiceName);
            if (action == "start" && status == ServiceControllerStatus.Running)
            {
                if (showNotifications)
                    MessageBox.Show("Redis service is already running.", "Redis", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (action == "stop" && status == ServiceControllerStatus.Stopped)
            {
                if (showNotifications)
                    MessageBox.Show("Redis service is already stopped.", "Redis", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                await RedisWindowsServiceManager.ControlAsync(_config.Redis.ServiceName, action);
                if (showNotifications)
                    MessageBox.Show($"Redis Windows service {action} completed.", "Redis", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            _redisPanel.SetBusy(false);
            await RefreshPanelAsync(_redisPanel);
        }
    }
}
