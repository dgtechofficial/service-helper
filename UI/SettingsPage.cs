using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using PostgresRedisManager.Models;

namespace PostgresRedisManager.UI;

internal sealed class SettingsPage : UserControl
{
    private const int FieldRow = 46;
    private const int CardChrome = 18 + 56 + 18; // top padding + title block + bottom padding
    private const int CardGap = 16;

    private readonly AppConfig _config;
    private readonly Func<Guid, bool> _isNodeAppRunning;
    private readonly Func<Guid, bool> _isPhpAppRunning;
    private readonly ModernTextBox _pgCtlPath;
    private readonly ModernTextBox _pgDataDirectory;
    private readonly ModernTextBox _pgHost;
    private readonly ModernTextBox _pgPort;
    private readonly ModernTextBox _redisServiceName;
    private readonly ModernTextBox _redisServerPath;
    private readonly ModernTextBox _redisCliPath;
    private readonly ModernTextBox _redisHost;
    private readonly ModernTextBox _redisPort;
    private readonly ModernTextBox _newAppName = new() { PlaceholderText = "App name" };
    private readonly ModernTextBox _newAppDirectory = new() { PlaceholderText = "Project folder containing package.json" };
    private readonly ModernTextBox _newAppRunCommand = new() { Text = "pnpm run start", PlaceholderText = "Run command" };
    private readonly ModernTextBox _newPhpAppName = new() { PlaceholderText = "App name" };
    private readonly ModernTextBox _newPhpAppDirectory = new() { PlaceholderText = "Project folder containing spark" };
    private readonly ModernTextBox _newPhpAppRunCommand = new() { Text = "php spark serve", PlaceholderText = "Run command" };
    private readonly FlowLayoutPanel _appsList = new();
    private readonly FlowLayoutPanel _phpAppsList = new();
    private readonly PagedTabControl _categoryTabs = new();
    private readonly ModernButton _saveServiceSettingsButton = new()
    {
        Text = "Save service settings",
        Kind = ButtonKind.Primary,
        SurfaceColor = Theme.PageBack
    };
    private readonly ToolTip _tips = new();
    private readonly System.Windows.Forms.Timer _savedTimer = new() { Interval = 3000 };

        private readonly StatusPill _savedPill = new() 
    {
        Text = "Service settings saved",
        Size = new Size(196, 28),
        Margin = new Padding(0, 4, 14, 0),
        Tone = Tone.Success,
        Visible = false
    };

    private RoundedPanel _appsCard = null!;
    private RoundedPanel _phpAppsCard = null!;

    public event EventHandler? ConfigurationChanged;

    public SettingsPage(AppConfig config, Func<Guid, bool> isNodeAppRunning, Func<Guid, bool> isPhpAppRunning)
    {
        _config = config;
        _isNodeAppRunning = isNodeAppRunning;
        _isPhpAppRunning = isPhpAppRunning;
        Dock = DockStyle.Fill;
        BackColor = Theme.PageBack;
        DoubleBuffered = true;

        _pgCtlPath = new ModernTextBox { Text = config.PostgreSQL.PgCtlPath };
        _pgDataDirectory = new ModernTextBox { Text = config.PostgreSQL.DataDirectory };
        _pgHost = new ModernTextBox { Text = config.PostgreSQL.Host };
        _pgPort = new ModernTextBox { Text = config.PostgreSQL.Port.ToString() };
        _redisServiceName = new ModernTextBox { Text = config.Redis.ServiceName };
        _redisServerPath = new ModernTextBox { Text = config.Redis.ServerPath };
        _redisCliPath = new ModernTextBox { Text = config.Redis.CliPath };
        _redisHost = new ModernTextBox { Text = config.Redis.Host };
        _redisPort = new ModernTextBox { Text = config.Redis.Port.ToString() };

        _savedTimer.Tick += (_, _) =>
        {
            _savedPill.Visible = false;
            _savedTimer.Stop();
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.PageBack,
            Margin = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(new NavStrip(_categoryTabs) { Dock = DockStyle.Fill }, 0, 1);
        _categoryTabs.Dock = DockStyle.Fill;
        _categoryTabs.BackColor = Theme.PageBack;
        root.Controls.Add(_categoryTabs, 0, 2);

        // PostgreSQL
        var (postgresCard, postgresBody) = CreateCard("PostgreSQL", "pg_ctl location, data directory and TCP endpoint.");
        var postgresGrid = CreateFieldGrid(3);
        AddPathRow(postgresGrid, 0, "pg_ctl.exe", _pgCtlPath, isDirectory: false);
        AddPathRow(postgresGrid, 1, "Data directory", _pgDataDirectory, isDirectory: true);
        AddHostPortRow(postgresGrid, 2, _pgHost, _pgPort);
        postgresBody.Controls.Add(postgresGrid);
        postgresCard.Height = CardChrome + 3 * FieldRow + CardGap;
        AddCategory("PostgreSQL", postgresCard);

        // Redis
        var (redisCard, redisBody) = CreateCard("Redis", "Windows service, executables and TCP endpoint.");
        var redisGrid = CreateFieldGrid(4);
        AddTextRow(redisGrid, 0, "Windows service name", _redisServiceName);
        AddPathRow(redisGrid, 1, "redis-server.exe", _redisServerPath, isDirectory: false);
        AddPathRow(redisGrid, 2, "redis-cli.exe", _redisCliPath, isDirectory: false);
        AddHostPortRow(redisGrid, 3, _redisHost, _redisPort);
        redisBody.Controls.Add(redisGrid);
        redisCard.Height = CardChrome + 4 * FieldRow + CardGap;
        AddCategory("Redis", redisCard);

        // Node applications
        var (appsCard, appsBody) = CreateCard("Node applications",
            "Apps listed on the Node apps tab. Stop an app before editing or removing it.");
        _appsCard = appsCard;
        BuildAppsBody(appsBody, isPhp: false);
        appsCard.Height = 300;
        AddCategory("Node apps", appsCard);

        var (phpAppsCard, phpAppsBody) = CreateCard("PHP applications",
            "Apps listed on the PHP apps tab. Stop an app before editing or removing it.");
        _phpAppsCard = phpAppsCard;
        BuildAppsBody(phpAppsBody, isPhp: true);
        phpAppsCard.Height = 300;
        AddCategory("PHP apps", phpAppsCard);
        _categoryTabs.SelectedIndex = 2;
        _saveServiceSettingsButton.Visible = false;
        _categoryTabs.SelectedIndexChanged += (_, _) =>
        {
            bool serviceSettingsSelected = _categoryTabs.SelectedIndex < 2;
            _saveServiceSettingsButton.Visible = serviceSettingsSelected;
            if (!serviceSettingsSelected)
                _savedPill.Visible = false;
        };

        Controls.Add(root);
        RebuildAppRows(isPhp: false);
        RebuildAppRows(isPhp: true);
    }

    private void AddCategory(string title, RoundedPanel card)
    {
        var page = new TabPage(title) { BackColor = Theme.PageBack };
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(28, 22, 28, 12),
            BackColor = Theme.PageBack
        };
        card.Width = 800;
        card.Dock = DockStyle.Top;
        card.Margin = new Padding(0, 0, 0, CardGap);
        scroll.Controls.Add(card);
        scroll.Resize += (_, _) => card.Width = Math.Max(1, scroll.ClientSize.Width - scroll.Padding.Horizontal);
        page.Controls.Add(scroll);
        _categoryTabs.TabPages.Add(page);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _savedTimer.Dispose();
            _tips.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Layout construction

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(28, 0, 28, 0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titles = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        titles.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        titles.Controls.Add(MakeLabel("Settings", Theme.Title, Theme.TextStrong, ContentAlignment.MiddleLeft), 0, 0);
        titles.Controls.Add(MakeLabel("Configure services and application launch settings.", Theme.Body, Theme.TextMuted, ContentAlignment.MiddleLeft), 0, 1);

        _saveServiceSettingsButton.Click += (_, _) => SaveServiceSettings();

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        actions.Controls.Add(_savedPill);
        actions.Controls.Add(_saveServiceSettingsButton);

        header.Controls.Add(titles, 0, 0);
        header.Controls.Add(actions, 1, 0);
        return header;
    }

    private void BuildAppsBody(Panel body, bool isPhp)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
                RowCount = 3, 
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, FieldRow));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var addRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        addRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var appName = isPhp ? _newPhpAppName : _newAppName;
        var appDirectory = isPhp ? _newPhpAppDirectory : _newAppDirectory;
        var appRunCommand = isPhp ? _newPhpAppRunCommand : _newAppRunCommand;
        var appList = isPhp ? _phpAppsList : _appsList;

        foreach (var input in new[] { appName, appDirectory, appRunCommand })
        {
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(0, 5, 10, 5);
        }

        var browse = FieldButton("Browse...", new Padding(0, 5, 8, 5));
        browse.Click += (_, _) => BrowsePath(appDirectory, isDirectory: true);
        var add = FieldButton("Add app", new Padding(0, 5, 0, 5), ButtonKind.Primary);
        add.Click += (_, _) => AddApp(isPhp);

        addRow.Controls.Add(appName, 0, 0);
        addRow.Controls.Add(appDirectory, 1, 0);
        addRow.Controls.Add(appRunCommand, 2, 0);
        addRow.Controls.Add(browse, 3, 0);
        addRow.Controls.Add(add, 4, 0);

        var divider = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Border, Margin = Padding.Empty };

        appList.Dock = DockStyle.Fill;
        appList.FlowDirection = FlowDirection.TopDown;
        appList.WrapContents = false;
        appList.AutoScroll = false;
        appList.Padding = new Padding(0, 12, 0, 0);
        appList.BackColor = Color.Transparent;
        appList.Resize += (_, _) => ResizeAppRows(isPhp);

        layout.Controls.Add(addRow, 0, 0);
        layout.Controls.Add(divider, 0, 1);
        layout.Controls.Add(appList, 0, 2);
        body.Controls.Add(layout);
    }

    private static (RoundedPanel Card, Panel Body) CreateCard(string title, string subtitle)
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, CardGap),
            Padding = new Padding(24, 18, 24, 18)
        };
        var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        var head = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 56,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        head.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        head.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        head.Controls.Add(MakeLabel(title, Theme.CardTitle, Theme.TextStrong, ContentAlignment.MiddleLeft), 0, 0);
        head.Controls.Add(MakeLabel(subtitle, Theme.Small, Theme.TextMuted, ContentAlignment.MiddleLeft), 0, 1);

        // Fill control first, then the docked header so the header ends up on top.
        card.Controls.Add(body);
        card.Controls.Add(head);
        return (card, body);
    }

    private static TableLayoutPanel CreateFieldGrid(int rowCount)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = rowCount,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        for (int index = 0; index < rowCount; index++)
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, FieldRow));
        return grid;
    }

    private static void AddTextRow(TableLayoutPanel grid, int index, string label, ModernTextBox input, bool span = true)
    {
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(0, 5, 0, 5);
        grid.Controls.Add(CreateFieldLabel(label), 0, index);
        grid.Controls.Add(input, 1, index);
        if (span)
            grid.SetColumnSpan(input, 2);
    }

    private void AddPathRow(TableLayoutPanel grid, int index, string label, ModernTextBox input, bool isDirectory)
    {
        AddTextRow(grid, index, label, input, span: false);
        var browse = FieldButton("Browse...", new Padding(10, 5, 0, 5));
        browse.Click += (_, _) => BrowsePath(input, isDirectory);
        grid.Controls.Add(browse, 2, index);
    }

    private static void AddHostPortRow(TableLayoutPanel grid, int index, ModernTextBox host, ModernTextBox port)
    {
        var pair = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        pair.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pair.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        pair.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        pair.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        host.Dock = DockStyle.Fill;
        host.Margin = new Padding(0, 5, 0, 5);
        port.Dock = DockStyle.Fill;
        port.Margin = new Padding(0, 5, 0, 5);
        var portLabel = CreateFieldLabel("Port");
        portLabel.TextAlign = ContentAlignment.MiddleRight;
        portLabel.Padding = new Padding(0, 0, 12, 0);

        pair.Controls.Add(host, 0, 0);
        pair.Controls.Add(portLabel, 1, 0);
        pair.Controls.Add(port, 2, 0);

        grid.Controls.Add(CreateFieldLabel("Host"), 0, index);
        grid.Controls.Add(pair, 1, index);
        grid.SetColumnSpan(pair, 2);
    }

    private static Label CreateFieldLabel(string text) =>
        MakeLabel(text, Theme.Body, Theme.TextBody, ContentAlignment.MiddleLeft);

    private static Label MakeLabel(string text, Font font, Color color, ContentAlignment align) => new()
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

    private static ModernButton FieldButton(string text, Padding margin, ButtonKind kind = ButtonKind.Secondary) => new()
    {
        Text = text,
        Kind = kind,
        AutoSize = false,
        Dock = DockStyle.Fill,
        Margin = margin
    };

    #endregion

    #region Applications

    private void AddApp(bool isPhp)
    {
        var nameInput = isPhp ? _newPhpAppName : _newAppName;
        var directoryInput = isPhp ? _newPhpAppDirectory : _newAppDirectory;
        var commandInput = isPhp ? _newPhpAppRunCommand : _newAppRunCommand;
        string name = nameInput.Text.Trim();
        string directory = directoryInput.Text.Trim();
        string runCommand = commandInput.Text.Trim();
        if (name.Length == 0 || directory.Length == 0 || runCommand.Length == 0)
        {
            ShowValidationError("Enter an app name, project folder, and run command.");
            return;
        }
        if (!Directory.Exists(directory))
        {
            ShowValidationError("The selected project folder does not exist.");
            return;
        }
        if (!File.Exists(Path.Combine(directory, isPhp ? "spark" : "package.json")))
        {
            ShowValidationError(isPhp
                ? "The selected project folder does not contain a spark file."
                : "The selected project folder does not contain package.json.");
            return;
        }
        bool duplicate = isPhp
            ? _config.PHPApps.Any(app => string.Equals(app.Directory, directory, StringComparison.OrdinalIgnoreCase))
            : _config.NodeApps.Any(app => string.Equals(app.Directory, directory, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            ShowValidationError("That project folder is already configured.");
            return;
        }

        string fullPath = Path.GetFullPath(directory);
        if (isPhp)
        {
            _config.PHPApps.Add(new PhpAppConfig { Name = name, Directory = fullPath, RunCommand = runCommand });
        }
        else
        {
            _config.NodeApps.Add(new NodeAppConfig { Name = name, Directory = fullPath, RunCommand = runCommand });
        }
        nameInput.Clear();
        directoryInput.Clear();
        RebuildAppRows(isPhp);
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildAppRows(bool isPhp)
    {
        List<AppProcessConfig> apps = isPhp
            ? _config.PHPApps.Select(app => new AppProcessConfig(app.Id, app.Name, app.Directory, app.RunCommand)).ToList()
            : _config.NodeApps.Select(app => new AppProcessConfig(app.Id, app.Name, app.Directory, app.RunCommand)).ToList();
        var appList = isPhp ? _phpAppsList : _appsList;
        var card = isPhp ? _phpAppsCard : _appsCard;
        appList.SuspendLayout();
        var old = appList.Controls.Cast<Control>().ToList();
        appList.Controls.Clear();
        if (old.Count > 0)
        {
            if (IsHandleCreated)
                BeginInvoke(() => old.ForEach(control => control.Dispose()));
            else
                old.ForEach(control => control.Dispose());
        }

        if (apps.Count == 0)
        {
            var empty = MakeLabel($"No {(isPhp ? "PHP" : "Node")} apps yet. Add a project folder above to get started.",
                Theme.Body, Theme.TextMuted, ContentAlignment.MiddleLeft);
            empty.Height = 44;
            appList.Controls.Add(empty);
        }
        else
        {
            foreach (var app in apps)
                appList.Controls.Add(CreateAppRow(new AppProcessConfig(app.Id, app.Name, app.Directory, app.RunCommand), isPhp));
        }

        ResizeAppRows(isPhp);
        appList.ResumeLayout();
        int listHeight = apps.Count == 0 ? 44 : apps.Count * 112;
        card.Height = 18 + 56 + FieldRow + 1 + 12 + listHeight + 18;
    }

    private Control CreateAppRow(AppProcessConfig app, bool isPhp)
    {
        bool running = (isPhp ? _isPhpAppRunning : _isNodeAppRunning)(app.Id);

        var row = new RoundedPanel
        {
            Height = 104,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(16, 8, 12, 8),
            Radius = 10,
            FillColor = Color.FromArgb(248, 250, 252)
        };
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        var name = new ModernTextBox
        {
            Text = app.Name,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0),
            Enabled = !running
        };
        var directory = new ModernTextBox
        {
            Text = app.Directory,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0),
            Enabled = !running
        };
        _tips.SetToolTip(directory, app.Directory);

        var runCommand = new ModernTextBox
        {
            Text = app.RunCommand,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 8, 2),
            Enabled = !running
        };
        var save = FieldButton("Save", new Padding(0, 2, 8, 2));
        var remove = FieldButton("Remove", new Padding(0, 2, 8, 2), ButtonKind.Danger);
        save.Enabled = !running;
        remove.Enabled = !running;
        save.Click += (_, _) => SaveAppConfiguration(app, name.Text, directory.Text, runCommand.Text, isPhp);
        remove.Click += (_, _) => RemoveApp(app, isPhp);

        grid.Controls.Add(name, 0, 0);
        grid.Controls.Add(directory, 0, 1);
        grid.Controls.Add(runCommand, 0, 2);
        grid.Controls.Add(save, 1, 2);
        grid.Controls.Add(remove, 2, 2);
        row.Controls.Add(grid);
        return row;
    }

    private void ResizeAppRows(bool isPhp)
    {
        var appList = isPhp ? _phpAppsList : _appsList;
        int width = Math.Max(1, appList.ClientSize.Width);
        foreach (Control control in appList.Controls)
            control.Width = width;
    }

    private void RemoveApp(AppProcessConfig app, bool isPhp)
    {
        var isRunning = isPhp ? _isPhpAppRunning : _isNodeAppRunning;
        if (isRunning(app.Id))
        {
            ShowValidationError($"Stop this app from the {(isPhp ? "PHP" : "Node")} Apps tab before removing it.");
            return;
        }
        if (isPhp)
            _config.PHPApps.RemoveAll(item => item.Id == app.Id);
        else
            _config.NodeApps.RemoveAll(item => item.Id == app.Id);
        RebuildAppRows(isPhp);
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SaveAppConfiguration(AppProcessConfig app, string name, string directory, string command, bool isPhp)
    {
        name = name.Trim();
        directory = directory.Trim();
        command = command.Trim();
        if (name.Length == 0 || directory.Length == 0 || command.Length == 0)
        {
            ShowValidationError("Enter an app name, project folder, and run command.");
            return;
        }
        if ((isPhp ? _isPhpAppRunning : _isNodeAppRunning)(app.Id))
        {
            ShowValidationError("Stop this app before changing its settings.");
            return;
        }

        if (!Directory.Exists(directory))
        {
            ShowValidationError("The selected project folder does not exist.");
            return;
        }

        string projectFile = isPhp ? "spark" : "package.json";
        if (!File.Exists(Path.Combine(directory, projectFile)))
        {
            ShowValidationError(isPhp
                ? "The selected project folder does not contain a spark file."
                : "The selected project folder does not contain package.json.");
            return;
        }

        string fullPath = Path.GetFullPath(directory);
        if (isPhp)
        {
            if (_config.PHPApps.Any(item => item.Id != app.Id && string.Equals(item.Directory, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                ShowValidationError("That project folder is already configured.");
                return;
            }

            var configuredApp = _config.PHPApps.Single(item => item.Id == app.Id);
            configuredApp.Name = name;
            configuredApp.Directory = fullPath;
            configuredApp.RunCommand = command;
        }
        else
        {
            if (_config.NodeApps.Any(item => item.Id != app.Id && string.Equals(item.Directory, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                ShowValidationError("That project folder is already configured.");
                return;
            }

            var configuredApp = _config.NodeApps.Single(item => item.Id == app.Id);
            configuredApp.Name = name;
            configuredApp.Directory = fullPath;
            configuredApp.RunCommand = command;
        }

        RebuildAppRows(isPhp);
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Service settings

    private void SaveServiceSettings()
    {
        if (!TryReadPort(_pgPort.Text, out int postgresPort) || !TryReadPort(_redisPort.Text, out int redisPort))
        {
            ShowValidationError("Both ports must be whole numbers between 1 and 65535.");
            return;
        }
        if (string.IsNullOrWhiteSpace(_redisServiceName.Text) || string.IsNullOrWhiteSpace(_pgHost.Text) || string.IsNullOrWhiteSpace(_redisHost.Text))
        {
            ShowValidationError("Service name and host values cannot be empty.");
            return;
        }

        _config.PostgreSQL.PgCtlPath = _pgCtlPath.Text.Trim();
        _config.PostgreSQL.DataDirectory = _pgDataDirectory.Text.Trim();
        _config.PostgreSQL.Host = _pgHost.Text.Trim();
        _config.PostgreSQL.Port = postgresPort;
        _config.Redis.ServiceName = _redisServiceName.Text.Trim();
        _config.Redis.ServerPath = _redisServerPath.Text.Trim();
        _config.Redis.CliPath = _redisCliPath.Text.Trim();
        _config.Redis.Host = _redisHost.Text.Trim();
        _config.Redis.Port = redisPort;
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);

        // Non-blocking confirmation instead of a modal message box.
        _savedPill.Visible = true;
        _savedTimer.Stop();
        _savedTimer.Start();
    }

    private static bool TryReadPort(string value, out int port) =>
        int.TryParse(value, out port) && port is >= 1 and <= 65535;

    private static void BrowsePath(ModernTextBox target, bool isDirectory)
    {
        if (isDirectory)
        {
            using var dialog = new FolderBrowserDialog();
            if (Directory.Exists(target.Text))
                dialog.InitialDirectory = target.Text;
            if (dialog.ShowDialog() == DialogResult.OK)
                target.Text = dialog.SelectedPath;
            return;
        }

        using var fileDialog = new OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (File.Exists(target.Text))
            fileDialog.InitialDirectory = Path.GetDirectoryName(target.Text);
        if (fileDialog.ShowDialog() == DialogResult.OK)
            target.Text = fileDialog.FileName;
    }

    private static void ShowValidationError(string message) =>
        MessageBox.Show(message, "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    #endregion
}

/// <summary>Rounded text input with a focus ring; wraps a borderless <see cref="TextBox"/>.</summary>
internal sealed class ModernTextBox : RoundedPanel
{
    private readonly TextBox _input = new() { BorderStyle = BorderStyle.None };

    public ModernTextBox()
    {
        Radius = 8;
        FillColor = Theme.Surface;
        BorderColor = Theme.Slate300;
        Size = new Size(200, 36);
        Cursor = Cursors.IBeam;

        _input.Font = Theme.Body;
        _input.ForeColor = Theme.TextStrong;
        _input.BackColor = Theme.Surface;
        _input.GotFocus += (_, _) => SetFocusRing(true);
        _input.LostFocus += (_, _) => SetFocusRing(false);
        _input.TextChanged += (_, _) => OnTextChanged(EventArgs.Empty);
        Controls.Add(_input);

        Click += (_, _) => _input.Focus();
    }

    [AllowNull]
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override string Text
    {
        get => _input.Text;
        set => _input.Text = value ?? string.Empty;
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string PlaceholderText
    {
        get => _input.PlaceholderText;
        set => _input.PlaceholderText = value;
    }

    public void Clear() => _input.Clear();

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        _input.SetBounds(12, Math.Max(0, (Height - _input.Height) / 2), Math.Max(0, Width - 24), _input.Height);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        var back = Enabled ? Theme.Surface : Theme.Slate100;
        FillColor = back;
        BorderColor = Enabled ? Theme.Slate300 : Theme.Slate200;
        _input.BackColor = back;
        _input.ForeColor = Enabled ? Theme.TextStrong : Theme.TextMuted;
        Invalidate();
    }

    private void SetFocusRing(bool focused)
    {
        BorderColor = focused && Enabled ? Theme.Primary : Enabled ? Theme.Slate300 : Theme.Slate200;
        Invalidate();
    }
}