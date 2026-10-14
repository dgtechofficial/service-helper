using PostgresRedisManager.Models;
using PostgresRedisManager.Services;

namespace PostgresRedisManager.UI;

internal sealed partial class MainForm
{
    private AppConfig LoadConfig()
    {
        try
        {
            return AppConfigStore.Load(_configPath);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Could not read appsettings.json. Default settings will be used.\n\n{exception.Message}",
                "Configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return AppConfig.CreateDefault();
        }
    }

    private void SaveConfig()
    {
        try
        {
            AppConfigStore.Save(_configPath, _config);
        }
        catch (Exception exception)
        {
            ShowError($"Could not save settings to {_configPath}.\n\n{exception.Message}");
        }
    }

    private void EditPostgresSettings()
    {
        using var dialog = new SettingsDialog("PostgreSQL settings",
        [
            ("pg_ctl.exe path", _config.PostgreSQL.PgCtlPath),
            ("Data directory", _config.PostgreSQL.DataDirectory),
            ("Host", _config.PostgreSQL.Host),
            ("TCP port", _config.PostgreSQL.Port.ToString())
        ]);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!int.TryParse(dialog.Values[3], out int port) || port is < 1 or > 65535)
        {
            ShowError("Port must be a number between 1 and 65535.");
            return;
        }

        _config.PostgreSQL.PgCtlPath = dialog.Values[0];
        _config.PostgreSQL.DataDirectory = dialog.Values[1];
        _config.PostgreSQL.Host = dialog.Values[2];
        _config.PostgreSQL.Port = port;
        SaveConfig();
        _postgresPanel.UpdateConfiguration(_config.PostgreSQL.Host, port,
            $"pg_ctl: {_config.PostgreSQL.PgCtlPath}", $"Data directory: {_config.PostgreSQL.DataDirectory}");
        _ = RefreshPanelAsync(_postgresPanel);
    }

    private void EditRedisSettings()
    {
        using var dialog = new SettingsDialog("Redis settings",
        [
            ("Windows service name", _config.Redis.ServiceName),
            ("redis-server.exe path", _config.Redis.ServerPath),
            ("redis-cli.exe path", _config.Redis.CliPath),
            ("Host", _config.Redis.Host),
            ("TCP port", _config.Redis.Port.ToString())
        ]);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (!int.TryParse(dialog.Values[4], out int port) || port is < 1 or > 65535)
        {
            ShowError("Port must be a number between 1 and 65535.");
            return;
        }

        _config.Redis.ServiceName = dialog.Values[0];
        _config.Redis.ServerPath = dialog.Values[1];
        _config.Redis.CliPath = dialog.Values[2];
        _config.Redis.Host = dialog.Values[3];
        _config.Redis.Port = port;
        SaveConfig();
        _redisPanel.UpdateConfiguration(_config.Redis.Host, port,
            $"Windows service: {_config.Redis.ServiceName}", $"Server: {_config.Redis.ServerPath}");
        _ = RefreshPanelAsync(_redisPanel);
    }

    private static void ShowError(string message) =>
        MessageBox.Show(message, "Services Manager", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
