
using PostgresRedisManager;
using PostgresRedisManager.Models;
using PostgresRedisManager.Services;

namespace PostgresRedisManager.UI;

internal sealed partial class MainForm : Form
{
    private readonly string _configPath =
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    private readonly AppConfig _config;
    private readonly ServicePanel _postgresPanel;
    private readonly ServicePanel _redisPanel;
    

    private readonly ModernButton _startBoth = new()
    {
        Text = "Start both",
        Icon = IconKind.Play,
        Kind = ButtonKind.Primary,
        SurfaceColor = Theme.PageBack,
        Margin = new Padding(0, 0, 8, 0)
    };

    private readonly ModernButton _stopBoth = new()
    {
        Text = "Stop both",
        Icon = IconKind.Stop,
        Kind = ButtonKind.Danger,
        SurfaceColor = Theme.PageBack,
        Margin = new Padding(0, 0, 8, 0)
    };

    private readonly ModernButton _refreshAll = new()
    {
        Text = "Refresh all",
        Icon = IconKind.Refresh,
        SurfaceColor = Theme.PageBack
    };

    private readonly ToggleSwitch _autoRefresh = new()
    {
        Text = "Auto-refresh every 5 seconds",
        SurfaceColor = Theme.PageBack,
        Margin = new Padding(18, 2, 0, 0)
    };

    private readonly System.Windows.Forms.Timer _timer = new()
    {
        Interval = 5000
    };

    private readonly SemaphoreSlim _serviceStatusGate = new(1, 1);
    private readonly AppProcessManager _appProcesses = new();

    private readonly PagedTabControl _tabs = new()
    {
        Dock = DockStyle.Fill
    };

    private AppManagementPage _nodeAppsPage = null!;
    private AppManagementPage _phpAppsPage = null!;

    private readonly Label _footer = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = false,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = Theme.Small,
        ForeColor = Theme.TextMuted,
        BackColor = Color.Transparent,
        UseMnemonic = false
    };

    private readonly StatusPill _healthPill = new()
    {
        Text = "Checking...",
        Size = new Size(176, 30),
        Anchor = AnchorStyles.Right
    };

    private bool? _postgresUp;
    private bool? _redisUp;
    private bool _refreshing;
    private bool _refreshPending;
    private bool _bulkOperating;
    private bool _closingApps;

    public MainForm()
    {
        _config = LoadConfig();

        Text = "Project Helper";
        // Set the application window and taskbar icon.
        Icon = new Icon(
            Path.Combine(AppContext.BaseDirectory, "app.ico")
        );
        ShowIcon = true;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 660);
        Size = new Size(1100, 780);
        Font = Theme.Body;
        BackColor = Theme.PageBack;
        DoubleBuffered = true;

        BuildHeader();
        BuildToolbar();

        // Service panels.
        _postgresPanel = new ServicePanel(
            "PostgreSQL 18",
            "PostgreSQL",
            _config.PostgreSQL.Port,
            _config.PostgreSQL.Host,
            $"pg_ctl: {_config.PostgreSQL.PgCtlPath}",
            $"Data directory: {_config.PostgreSQL.DataDirectory}");

        _redisPanel = new ServicePanel(
            "Redis",
            "Redis",
            _config.Redis.Port,
            _config.Redis.Host,
            $"Windows service: {_config.Redis.ServiceName}",
            $"Server: {_config.Redis.ServerPath}");

        WireServiceEvents();

        // Tabs.
        var servicesTab = new TabPage("Services")
        {
            BackColor = Theme.PageBack
        };

        var nodeAppsTab = new TabPage("Node apps")
        {
            BackColor = Theme.PageBack
        };

        var phpAppsTab = new TabPage("PHP apps")
        {
            BackColor = Theme.PageBack
        };

        var settingsTab = new TabPage("Settings")
        {
            BackColor = Theme.PageBack
        };

        _nodeAppsPage = new AppManagementPage(_appProcesses, "Node", "package.json");
        _phpAppsPage = new AppManagementPage(_appProcesses, "PHP", "spark");
        _nodeAppsPage.SetApps(_config.NodeApps.Select(ToProcessConfig).ToList());
        _phpAppsPage.SetApps(_config.PHPApps.Select(ToProcessConfig).ToList());

        var settingsPage = new SettingsPage(
            _config,
            _nodeAppsPage.IsRunning,
            _phpAppsPage.IsRunning);

        settingsPage.ConfigurationChanged += (_, _) =>
        {
            SaveConfig();
            _nodeAppsPage.SetApps(_config.NodeApps.Select(ToProcessConfig).ToList());
            _phpAppsPage.SetApps(_config.PHPApps.Select(ToProcessConfig).ToList());

            _postgresPanel.UpdateConfiguration(
                _config.PostgreSQL.Host,
                _config.PostgreSQL.Port,
                $"pg_ctl: {_config.PostgreSQL.PgCtlPath}",
                $"Data directory: {_config.PostgreSQL.DataDirectory}");

            _redisPanel.UpdateConfiguration(
                _config.Redis.Host,
                _config.Redis.Port,
                $"Windows service: {_config.Redis.ServiceName}",
                $"Server: {_config.Redis.ServerPath}");

            _ = RefreshAllAsync();
        };

        // Scrollable service area.
        var content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 8, 24, 16),
            BackColor = Theme.PageBack,
            AutoScroll = true
        };

        var serviceStack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.PageBack
        };

        serviceStack.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100F));

        serviceStack.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));

        serviceStack.RowStyles.Add(
            new RowStyle(SizeType.AutoSize));

        ConfigureServicePanel(_postgresPanel);
        ConfigureServicePanel(_redisPanel);

        serviceStack.Controls.Add(_postgresPanel, 0, 0);
        serviceStack.Controls.Add(_redisPanel, 0, 1);

        content.Controls.Add(serviceStack);

        servicesTab.Controls.Add(content);

        // Add toolbar after content so DockStyle.Top reserves its space.
        var toolbar = CreateToolbar();
        servicesTab.Controls.Add(toolbar);

        nodeAppsTab.Controls.Add(_nodeAppsPage);
        phpAppsTab.Controls.Add(_phpAppsPage);
        settingsTab.Controls.Add(settingsPage);

        _tabs.TabPages.AddRange(
        [
            servicesTab,
            nodeAppsTab,
            phpAppsTab,
            settingsTab
        ]);

        var nav = new NavStrip(_tabs)
        {
            Dock = DockStyle.Top
        };

        // Footer.
        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            Padding = new Padding(24, 0, 24, 0),
            BackColor = Theme.Surface
        };

        footer.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Border);

            e.Graphics.DrawLine(
                pen,
                0,
                0,
                footer.Width,
                0);
        };

        footer.Resize += (_, _) => footer.Invalidate();
        footer.Controls.Add(_footer);

        // Dock order: fill content first, then navigation and header.
        Controls.Add(_tabs);
        Controls.Add(nav);
        Controls.Add(BuildHeaderPanel());
        Controls.Add(footer);

        // Events and lifecycle.
        _timer.Tick += async (_, _) => await RefreshAllAsync();

        FormClosing += async (_, eventArgs) =>
        {
            if (_closingApps || (!_nodeAppsPage.HasRunningApps && !_phpAppsPage.HasRunningApps))
                return;

            var answer = MessageBox.Show(
                "Stop the Node and PHP apps started by this manager and close? Select No to keep the manager open.",
                "Applications are running",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (answer != DialogResult.Yes)
            {
                eventArgs.Cancel = true;
                return;
            }

            eventArgs.Cancel = true;
            _closingApps = true;

            await _nodeAppsPage.StopAllAsync();

            Close();
        };

        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _timer.Dispose();
            _appProcesses.Dispose();
        };

        Shown += async (_, _) =>
        {
            if (!File.Exists(_configPath))
                SaveConfig();

            await RefreshAllAsync();
        };
    }

    private void BuildHeader()
    {
        // Header is created by BuildHeaderPanel and added to the form.
    }

    private Panel BuildHeaderPanel()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            Padding = new Padding(24, 0, 24, 0),
            BackColor = Theme.Surface
        };

        var headerGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };

        headerGrid.ColumnStyles.Add(
            new ColumnStyle(SizeType.AutoSize));

        headerGrid.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100F));

        headerGrid.ColumnStyles.Add(
            new ColumnStyle(SizeType.AutoSize));

        headerGrid.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100F));

        var brand = new BrandMark
        {
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 14, 0)
        };

        var titles = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };

        titles.RowStyles.Add(
            new RowStyle(SizeType.Percent, 56F));

        titles.RowStyles.Add(
            new RowStyle(SizeType.Percent, 44F));

        titles.Controls.Add(
            HeaderLabel(
                "App Services",
                Theme.Title,
                Theme.TextStrong,
                ContentAlignment.BottomLeft),
            0, 0);

        titles.Controls.Add(
            HeaderLabel(
                "PostgreSQL, Redis, Node and PHP applications",
                Theme.Body,
                Theme.TextMuted,
                ContentAlignment.TopLeft),
            0, 1);

        headerGrid.Controls.Add(brand, 0, 0);
        headerGrid.Controls.Add(titles, 1, 0);
        headerGrid.Controls.Add(_healthPill, 2, 0);

        header.Controls.Add(headerGrid);

        return header;
    }

    private void BuildToolbar()
    {
        _startBoth.Click += async (_, _) => await StartBothAsync();
        _stopBoth.Click += async (_, _) => await StopBothAsync();
        _refreshAll.Click += async (_, _) => await RefreshAllAsync();

        _autoRefresh.CheckedChanged += (_, _) =>
        {
            _timer.Enabled = _autoRefresh.Checked;
        };
    }

    private FlowLayoutPanel CreateToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 64,
            Padding = new Padding(24, 10, 12, 6),
            BackColor = Theme.PageBack,
            WrapContents = true,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight
        };

        toolbar.Controls.AddRange(
        [
            _startBoth,
            _stopBoth,
            _refreshAll,
            _autoRefresh
        ]);

        return toolbar;
    }

    private void WireServiceEvents()
    {
        _postgresPanel.StartClicked += async (_, _) =>
            await StartPostgresAsync();

        _postgresPanel.StopClicked += async (_, _) =>
            await StopPostgresAsync();

        _postgresPanel.RefreshClicked += async (_, _) =>
            await RefreshPanelAsync(_postgresPanel);

        _postgresPanel.SettingsClicked += (_, _) =>
            _tabs.SelectedIndex = 3;

        _redisPanel.StartClicked += async (_, _) =>
            await ControlRedisAsync("start");

        _redisPanel.StopClicked += async (_, _) =>
            await ControlRedisAsync("stop");

        _redisPanel.RefreshClicked += async (_, _) =>
            await RefreshPanelAsync(_redisPanel);

        _redisPanel.SettingsClicked += (_, _) =>
            _tabs.SelectedIndex = 3;
    }

    private static void ConfigureServicePanel(ServicePanel panel)
    {
        panel.Dock = DockStyle.Top;
        panel.Height = 220;
        panel.MinimumSize = new Size(280, 220);
        panel.Margin = new Padding(0, 0, 0, 12);
    }

    private async Task RefreshAllAsync()
    {
        if (_refreshing || IsDisposed)
        {
            _refreshPending = true;
            return;
        }

        _refreshing = true;
        _refreshAll.Enabled = false;

        _footer.Text =
            "Checking both services and their TCP endpoints...";

        try
        {
            await RefreshPanelCoreAsync(
                _postgresPanel,
                _config.PostgreSQL.Host,
                _config.PostgreSQL.Port);

            await RefreshPanelCoreAsync(
                _redisPanel,
                _config.Redis.Host,
                _config.Redis.Port);

            _footer.Text =
                $"Last checked: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }
        finally
        {
            _refreshing = false;

            if (!IsDisposed)
                _refreshAll.Enabled = true;
        }

        if (_refreshPending && !IsDisposed)
        {
            _refreshPending = false;
            await RefreshAllAsync();
        }
    }

    private async Task RefreshPanelAsync(ServicePanel panel)
    {
        if (IsDisposed)
            return;

        if (_refreshing)
        {
            _refreshPending = true;
            return;
        }

        bool postgres = panel.ServiceKey == "PostgreSQL";

        await RefreshPanelCoreAsync(
            panel,
            postgres
                ? _config.PostgreSQL.Host
                : _config.Redis.Host,
            postgres
                ? _config.PostgreSQL.Port
                : _config.Redis.Port);

        _footer.Text =
            $"Last checked: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
    }

    private async Task RefreshPanelCoreAsync(
        ServicePanel panel,
        string host,
        int port)
    {
        await _serviceStatusGate.WaitAsync();
        if (IsDisposed || panel.IsDisposed)
        {
            _serviceStatusGate.Release();
            return;
        }

        panel.SetBusy(true);

        try
        {
            bool endpointReachable = await TcpPortProbe.IsOpenAsync(
                host,
                port,
                TimeSpan.FromSeconds(1));

            string status;
            string details;

            if (panel.ServiceKey == "PostgreSQL")
            {
                bool clusterRunning = false;
                string clusterStatus;
                try
                {
                    var pgStatus = await PostgreSqlProcessManager.GetStatusAsync(_config.PostgreSQL);
                    clusterRunning = pgStatus.ExitCode == 0;
                    clusterStatus = clusterRunning
                        ? "pg_ctl confirms the configured cluster is running."
                        : "pg_ctl reports the configured cluster is stopped.";
                }
                catch (Exception exception)
                {
                    clusterStatus = $"Could not check the configured cluster: {exception.Message}";
                }

                bool running = clusterRunning || endpointReachable;
                SetReachability(panel, running);
                bool dataExists =
                    Directory.Exists(_config.PostgreSQL.DataDirectory);

                status = running
                    ? clusterRunning
                        ? "RUNNING | configured PostgreSQL cluster is active"
                        : "RUNNING | configured TCP endpoint accepts connections"
                    : "STOPPED | no cluster or TCP listener detected";

                details = dataExists
                    ? $"Data directory found: {_config.PostgreSQL.DataDirectory}"
                    : $"Data directory not found: {_config.PostgreSQL.DataDirectory}";

                details += endpointReachable
                    ? $" | TCP reachable at {host}:{port}"
                    : $" | TCP not reachable at {host}:{port}";
                details += $" | {clusterStatus}";

                if (!File.Exists(_config.PostgreSQL.PgCtlPath))
                {
                    details +=
                        $" | pg_ctl.exe not found: {_config.PostgreSQL.PgCtlPath}";
                }
            }
            else
            {
                SetReachability(panel, endpointReachable);
                string serviceStatus;
                string serviceDetails;

                try
                {
                    serviceStatus =
                        RedisWindowsServiceManager
                            .GetStatus(_config.Redis.ServiceName)
                            .ToString();

                    serviceDetails =
                        $"Windows service: {_config.Redis.ServiceName}";
                }
                catch (Exception exception)
                {
                    serviceStatus = "Unavailable";
                    serviceDetails = exception.Message;
                }

                status = endpointReachable
                    ? $"TCP REACHABLE | Redis service {serviceStatus}"
                    : $"TCP NOT RESPONDING | Redis service {serviceStatus}";

                details =
                    $"{serviceDetails} | Server: " +
                    (File.Exists(_config.Redis.ServerPath)
                        ? "executable found"
                        : "executable path not found");
            }

            panel.ShowStatus(status, details, panel.ServiceKey == "PostgreSQL"
                ? _postgresUp == true
                : endpointReachable);
        }
        catch (Exception exception)
        {
            SetReachability(panel, false);
            panel.ShowError(exception.Message);
        }
        finally
        {
            if (!panel.IsDisposed)
                panel.SetBusy(false);
            _serviceStatusGate.Release();
        }
    }

    private void SetBulkBusy(bool busy)
    {
        _bulkOperating = busy;

        _startBoth.Enabled = !busy;
        _stopBoth.Enabled = !busy;
    }

    private void SetReachability(
        ServicePanel panel,
        bool reachable)
    {
        if (panel.ServiceKey == "PostgreSQL")
            _postgresUp = reachable;
        else
            _redisUp = reachable;

        UpdateHealthPill();
    }

    private void UpdateHealthPill()
    {
        if (_postgresUp is null || _redisUp is null)
        {
            _healthPill.Text = "Checking...";
            _healthPill.Tone = Tone.Neutral;
            return;
        }

        int up =
            (_postgresUp.Value ? 1 : 0) +
            (_redisUp.Value ? 1 : 0);

        (_healthPill.Text, _healthPill.Tone) = up switch
        {
            2 => ("All services running", Tone.Success),
            0 => ("Services not responding", Tone.Danger),
            _ => ("1 of 2 services running", Tone.Warning)
        };
    }

    private static Label HeaderLabel(
        string text,
        Font font,
        Color color,
        ContentAlignment align) => new()
    {
        Text = text,
        Font = font,
        ForeColor = color,
        Dock = DockStyle.Fill,
        AutoSize = false,
        AutoEllipsis = true,
        BackColor = Color.Transparent,
        TextAlign = align,
        Margin = Padding.Empty,
        UseMnemonic = false
    };

    private static AppProcessConfig ToProcessConfig(NodeAppConfig app) =>
        new(app.Id, app.Name, app.Directory, app.RunCommand);

    private static AppProcessConfig ToProcessConfig(PhpAppConfig app) =>
        new(app.Id, app.Name, app.Directory, app.RunCommand);
}
