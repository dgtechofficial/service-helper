using PostgresRedisManager.Models;
using PostgresRedisManager.Services;

namespace PostgresRedisManager.UI;

internal sealed class AppManagementPage : UserControl
{
    private readonly AppProcessManager _processManager;
    private readonly string _appType;
    private readonly string _projectFile;
    private readonly FlowLayoutPanel _appList = new();
    private readonly RichTextBox _output = new();
    private readonly Label _emptyState = new();
    private readonly Label _runningSummary = new();
    private readonly SplitContainer _split = new();
    private readonly ToolTip _tips = new();
    private readonly Dictionary<Guid, AppRow> _rows = new();
    private IReadOnlyList<AppProcessConfig> _apps = [];
    private bool _splitReady;

    public AppManagementPage(AppProcessManager processManager, string appType, string projectFile)
    {
        _processManager = processManager;
        _appType = appType;
        _projectFile = projectFile;
        Dock = DockStyle.Fill;
        BackColor = Theme.PageBack;
        DoubleBuffered = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(24, 20, 24, 20),
            BackColor = Theme.PageBack
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildBody(), 0, 1);
        Controls.Add(root);
        _processManager.OutputReceived += OnOutputReceived;
        _processManager.StateChanged += OnStateChanged;
    }

    public bool IsRunning(Guid appId) => _processManager.IsRunning(appId);
    public bool HasRunningApps => _apps.Any(app => _processManager.IsRunning(app.Id));
    public Task StopAllAsync() => _processManager.StopAllAsync();

    public void SetApps(IReadOnlyList<AppProcessConfig> apps)
    {
        _apps = apps;
        _rows.Clear();
        _appList.Controls.Clear();
        bool empty = apps.Count == 0;
        _emptyState.Visible = empty;
        _appList.Visible = !empty;
        foreach (var app in apps)
        {
            var row = CreateRow(app);
            _rows[app.Id] = row;
            _appList.Controls.Add(row.Container);
            SetAppState(app.Id, _processManager.IsRunning(app.Id) ? "Running" : "Stopped");
        }
        ResizeRows();
        UpdateRunningSummary();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _processManager.OutputReceived -= OnOutputReceived;
            _processManager.StateChanged -= OnStateChanged;
            _tips.Dispose();
        }
        base.Dispose(disposing);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.PageBack,
            Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
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
        titles.Controls.Add(CreateLabel($"{_appType} applications", Theme.Title, Theme.TextStrong), 0, 0);
        titles.Controls.Add(CreateLabel("Start, stop and monitor your local projects.", Theme.Body, Theme.TextMuted), 0, 1);
        _runningSummary.AutoSize = true;
        _runningSummary.Font = Theme.Body;
        _runningSummary.ForeColor = Theme.TextMuted;
        _runningSummary.BackColor = Color.Transparent;
        _runningSummary.Margin = Padding.Empty;
        _runningSummary.UseMnemonic = false;
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
        actions.Controls.Add(_runningSummary);
        header.Controls.Add(titles, 0, 0);
        header.Controls.Add(actions, 1, 0);
        return header;
    }

    private Control BuildBody()
    {
        _split.Dock = DockStyle.Fill;
        _split.Orientation = Orientation.Vertical;
        _split.SplitterWidth = 12;
        _split.BackColor = Theme.PageBack;
        _split.Panel1.BackColor = Theme.PageBack;
        _split.Panel2.BackColor = Theme.PageBack;
        _split.SizeChanged += (_, _) => InitSplit();
        _appList.Dock = DockStyle.Fill;
        _appList.FlowDirection = FlowDirection.TopDown;
        _appList.WrapContents = false;
        _appList.AutoScroll = true;
        _appList.BackColor = Theme.PageBack;
        _appList.Resize += (_, _) => ResizeRows();
        _emptyState.Text = $"No {_appType} configured yet.{Environment.NewLine}Add project folders in Settings to get started.";
        _emptyState.Dock = DockStyle.Fill;
        _emptyState.TextAlign = ContentAlignment.MiddleCenter;
        _emptyState.Font = Theme.Body;
        _emptyState.ForeColor = Theme.TextMuted;
        _emptyState.BackColor = Theme.PageBack;
        _emptyState.UseMnemonic = false;
        _split.Panel1.Controls.Add(_appList);
        _split.Panel1.Controls.Add(_emptyState);

        var activity = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 16) };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var logHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var clear = new ModernButton { Icon = IconKind.Clear, Anchor = AnchorStyles.Right, AccessibleName = "Clear activity log" };
        _tips.SetToolTip(clear, "Clear activity log");
        clear.Click += (_, _) => _output.Clear();
        logHeader.Controls.Add(CreateLabel("Activity", Theme.CardTitle, Theme.TextStrong), 0, 0);
        logHeader.Controls.Add(clear, 1, 0);
        var terminal = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            Radius = 8,
            FillColor = Theme.LogBack,
            BorderColor = Theme.LogBack,
            Padding = new Padding(14, 10, 8, 10)
        };
        _output.Dock = DockStyle.Fill;
        _output.ReadOnly = true;
        _output.BorderStyle = BorderStyle.None;
        _output.BackColor = Theme.LogBack;
        _output.ForeColor = Theme.LogText;
        _output.Font = Theme.Mono;
        _output.ScrollBars = RichTextBoxScrollBars.Vertical;
        _output.DetectUrls = false;
        _output.MaxLength = 60000;
        terminal.Controls.Add(_output);
        layout.Controls.Add(logHeader, 0, 0);
        layout.Controls.Add(terminal, 0, 1);
        activity.Controls.Add(layout);
        _split.Panel2.Controls.Add(activity);
        return _split;
    }

    private void InitSplit()
    {
        if (_splitReady || _split.Width < 760) return;
        _splitReady = true;
        _split.Panel1MinSize = 400;
        _split.Panel2MinSize = 280;
        _split.SplitterDistance = (int)(_split.Width * 0.56);
    }

    private AppRow CreateRow(AppProcessConfig app)
    {
        var card = new RoundedPanel
        {
            Height = 92,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(28, 10, 16, 10),
            AccentColor = Theme.Slate300
        };
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var info = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        info.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        info.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        info.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        var name = CreateLabel(app.Name, Theme.CardTitle, Theme.TextStrong);
        var path = CreateLabel(app.Directory, Theme.Small, Theme.TextMuted);
        var command = CreateLabel(app.RunCommand, Theme.Mono, Theme.Primary);
        _tips.SetToolTip(path, app.Directory);
        _tips.SetToolTip(command, app.RunCommand);
        info.Controls.Add(name, 0, 0);
        info.Controls.Add(path, 0, 1);
        info.Controls.Add(command, 0, 2);
        var status = new StatusPill { Text = "Stopped", Anchor = AnchorStyles.Right, Margin = new Padding(0, 0, 14, 0) };
        var start = new ModernButton
        {
            Icon = IconKind.Play,
            Kind = ButtonKind.Primary,
            Margin = new Padding(0, 0, 8, 0),
            AccessibleName = $"Start {app.Name}"
        };
        var stop = new ModernButton { Icon = IconKind.Stop, Kind = ButtonKind.Danger, Enabled = false, AccessibleName = $"Stop {app.Name}" };
        _tips.SetToolTip(start, $"Start {app.Name}");
        _tips.SetToolTip(stop, $"Stop {app.Name}");
        start.Click += (_, _) => StartApp(app);
        stop.Click += async (_, _) => await StopAppAsync(app);
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
        actions.Controls.Add(start);
        actions.Controls.Add(stop);
        grid.Controls.Add(info, 0, 0);
        grid.Controls.Add(status, 1, 0);
        grid.Controls.Add(actions, 2, 0);
        card.Controls.Add(grid);
        return new AppRow(card, status, start, stop);
    }

    private void StartApp(AppProcessConfig app)
    {
        try { _processManager.Start(app, _projectFile, _appType); }
        catch (Exception exception)
        {
            SetAppState(app.Id, "Start failed");
            AppendOutput(app.Name, exception.Message);
        }
    }

    private async Task StopAppAsync(AppProcessConfig app)
    {
        SetAppState(app.Id, "Stopping...");
        try { await _processManager.StopAsync(app.Id); }
        catch (Exception exception)
        {
            AppendOutput(app.Name, exception.Message);
            SetAppState(app.Id, "Stop failed");
        }
    }

    private void OnOutputReceived(object? sender, AppProcessEventArgs eventArgs)
    {
        if (!_apps.Any(app => app.Id == eventArgs.AppId)) return;
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            BeginInvoke(() => OnOutputReceived(sender, eventArgs));
            return;
        }
        AppendOutput(eventArgs.AppName, eventArgs.Message);
    }

    private void OnStateChanged(object? sender, AppProcessEventArgs eventArgs)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            BeginInvoke(() => OnStateChanged(sender, eventArgs));
            return;
        }
        SetAppState(eventArgs.AppId, eventArgs.Message);
    }

    private void SetAppState(Guid appId, string state)
    {
        if (!_rows.TryGetValue(appId, out var row)) return;
        bool running = _processManager.IsRunning(appId);
        bool failed = state.Contains("fail", StringComparison.OrdinalIgnoreCase) || state.Contains("error", StringComparison.OrdinalIgnoreCase);
        bool transitioning = state.StartsWith("Stopping", StringComparison.OrdinalIgnoreCase) || state.StartsWith("Starting", StringComparison.OrdinalIgnoreCase);
        var tone = failed ? Tone.Danger : transitioning ? Tone.Warning : running ? Tone.Success : Tone.Neutral;
        row.Status.Text = state;
        row.Status.Tone = tone;
        row.Container.AccentColor = Theme.Colors(tone).Accent;
        row.Start.Enabled = !running;
        row.Stop.Enabled = running && !state.StartsWith("Stopping", StringComparison.OrdinalIgnoreCase);
        UpdateRunningSummary();
    }

    private void UpdateRunningSummary()
    {
        int runningCount = _apps.Count(app => _processManager.IsRunning(app.Id));
        _runningSummary.Text = $"{runningCount} running / {_apps.Count} configured";
    }

    private void AppendOutput(string appName, string line)
    {
        AppendSegment($"{DateTime.Now:HH:mm:ss}  ", Theme.LogMuted);
        AppendSegment($"{appName}  ", AppColor(appName));
        AppendSegment(line + Environment.NewLine, MessageColor(line));
        if (_output.TextLength > 50000)
        {
            int cut = _output.Text.IndexOf('\n', _output.TextLength - 40000);
            _output.Select(0, cut < 0 ? _output.TextLength - 40000 : cut + 1);
            _output.SelectedText = "";
        }
        _output.SelectionStart = _output.TextLength;
        _output.ScrollToCaret();
    }

    private void AppendSegment(string text, Color color)
    {
        _output.SelectionStart = _output.TextLength;
        _output.SelectionLength = 0;
        _output.SelectionColor = color;
        _output.AppendText(text);
    }

    private void ResizeRows()
    {
        int width = Math.Max(360, _appList.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);
        foreach (var row in _rows.Values)
            row.Container.Width = width;
    }

    private static Label CreateLabel(string text, Font font, Color color) => new()
    {
        Text = text,
        Font = font,
        ForeColor = color,
        Dock = DockStyle.Fill,
        AutoSize = false,
        AutoEllipsis = true,
        BackColor = Color.Transparent,
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = Padding.Empty,
        UseMnemonic = false
    };

    private static Color AppColor(string appName) =>
        Theme.LogAppPalette[(int)((uint)appName.GetHashCode() % (uint)Theme.LogAppPalette.Length)];

    private static Color MessageColor(string line)
    {
        if (line.Contains("error", StringComparison.OrdinalIgnoreCase) || line.Contains("fail", StringComparison.OrdinalIgnoreCase) || line.Contains("exception", StringComparison.OrdinalIgnoreCase))
            return Theme.LogError;
        if (line.Contains("warn", StringComparison.OrdinalIgnoreCase))
            return Theme.LogWarn;
        return Theme.LogText;
    }

    private sealed record AppRow(RoundedPanel Container, StatusPill Status, ModernButton Start, ModernButton Stop);
}