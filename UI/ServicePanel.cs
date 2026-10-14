namespace PostgresRedisManager.UI;

internal sealed class ServicePanel : RoundedPanel
{
    private readonly string _title;
    private readonly Label _configuration = new();
    private readonly Label _diagnostics = new();
    private readonly Label _endpoint = new();
    private readonly StatusPill _status = new();
    private readonly ModernButton _start = new()
    {
        Text = "Start",
        Icon = IconKind.Play,
        Kind = ButtonKind.Primary
    };
    private readonly ModernButton _stop = new()
    {
        Text = "Stop",
        Icon = IconKind.Stop,
        Kind = ButtonKind.Danger
    };
    private readonly ModernButton _refresh = new()
    {
        Icon = IconKind.Refresh,
        AccessibleName = "Check service status"
    };
    private readonly ModernButton _settings = new()
    {
        Text = "Settings"
    };
    private readonly Label _busy = new();
    private bool? _isRunning;
    private bool _isBusy;

    public string ServiceKey { get; }
    public event EventHandler? StartClicked;
    public event EventHandler? StopClicked;
    public event EventHandler? RefreshClicked;
    public event EventHandler? SettingsClicked;

    public ServicePanel(string title, string key, int port, string host, string line1, string line2)
    {
        _title = title;
        ServiceKey = key;
        Dock = DockStyle.Top;
        Height = 220;
        MinimumSize = new Size(280, 220);
        Margin = new Padding(0, 0, 0, 12);
        Padding = new Padding(24, 16, 20, 14);
        Radius = 12;
        FillColor = Theme.Surface;
        BorderColor = Theme.Border;
        AccentColor = Theme.Slate300;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = CreateLabel(_title, Theme.CardTitle, Theme.TextStrong);
        titleLabel.Font = new Font(Theme.CardTitle.FontFamily, 13F, FontStyle.Bold);
        _status.Size = new Size(142, 28);
        _status.Anchor = AnchorStyles.Right;
        _status.Tone = Tone.Neutral;
        _status.Text = "Checking";
        header.Controls.Add(titleLabel, 0, 0);
        header.Controls.Add(_status, 1, 0);

        _endpoint = CreateLabel("", Theme.BodyBold, Theme.TextBody);
        _configuration = CreateLabel("", Theme.Small, Theme.TextMuted);
        _diagnostics = CreateLabel("Waiting for the first status check.", Theme.Small, Theme.TextMuted);
        _diagnostics.AutoEllipsis = true;

        var actionBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actionBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var serviceActions = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        var utilityActions = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Anchor = AnchorStyles.Right
        };
        _start.SurfaceColor = Theme.Surface;
        _stop.SurfaceColor = Theme.Surface;
        _refresh.SurfaceColor = Theme.Surface;
        _settings.SurfaceColor = Theme.Surface;
        _start.Margin = new Padding(0, 0, 10, 0);
        _stop.Margin = new Padding(0);
        _refresh.Margin = new Padding(0, 0, 10, 0);
        _settings.Margin = new Padding(0);
        _start.Click += (_, _) => StartClicked?.Invoke(this, EventArgs.Empty);
        _stop.Click += (_, _) => StopClicked?.Invoke(this, EventArgs.Empty);
        _refresh.Click += (_, _) => RefreshClicked?.Invoke(this, EventArgs.Empty);
        _settings.Click += (_, _) => SettingsClicked?.Invoke(this, EventArgs.Empty);
        serviceActions.Controls.AddRange([_start, _stop]);
        utilityActions.Controls.AddRange([_refresh, _settings]);

        _busy.Dock = DockStyle.Fill;
        _busy.Font = Theme.Small;
        _busy.ForeColor = Theme.TextMuted;
        _busy.TextAlign = ContentAlignment.MiddleRight;
        _busy.UseMnemonic = false;

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(_endpoint, 0, 1);
        layout.Controls.Add(_configuration, 0, 2);
        layout.Controls.Add(_diagnostics, 0, 3);
        actionBar.Controls.Add(serviceActions, 0, 0);
        actionBar.Controls.Add(_busy, 1, 0);
        actionBar.Controls.Add(utilityActions, 2, 0);
        layout.Controls.Add(actionBar, 0, 4);
        Controls.Add(layout);

        UpdateConfiguration(host, port, line1, line2);
    }

    public void UpdateConfiguration(string host, int port, string line1, string line2)
    {
        _endpoint.Text = $"{host}:{port}";
        _configuration.Text = $"{line1}  |  {line2}";
        _configuration.AutoEllipsis = true;
    }

    public void ShowStatus(string status, string details, bool healthy)
    {
        _isRunning = healthy;
        _status.Text = healthy ? "Running" : "Stopped";
        _status.Tone = healthy ? Tone.Success : Tone.Warning;
        AccentColor = healthy ? Theme.Colors(Tone.Success).Accent : Theme.Colors(Tone.Warning).Accent;
        _diagnostics.Text = $"{status}{Environment.NewLine}{details}";
        UpdateActionStates();
    }

    public void ShowError(string message)
    {
        _isRunning = null;
        _status.Text = "Error";
        _status.Tone = Tone.Danger;
        AccentColor = Theme.Colors(Tone.Danger).Accent;
        _diagnostics.Text = message;
        UpdateActionStates();
    }

    public void SetBusy(bool busy)
    {
        _isBusy = busy;
        _busy.Text = busy ? "Working..." : "";
        UpdateActionStates();
        _refresh.Enabled = !busy;
        _settings.Enabled = !busy;
    }

    private void UpdateActionStates()
    {
        _start.Enabled = !_isBusy && _isRunning != true;
        _stop.Enabled = !_isBusy && _isRunning != false;
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
}
