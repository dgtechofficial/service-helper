using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace PostgresRedisManager.UI;

#region Theme

internal enum Tone { Neutral, Success, Warning, Danger }

internal static class Theme
{
    private static readonly string SansFamily =
        FontFamily.Families.Any(f => f.Name == "Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";

    private static readonly string MonoFamily =
        FontFamily.Families.Any(f => f.Name == "Cascadia Mono") ? "Cascadia Mono" : "Consolas";

    // Surfaces
    public static readonly Color PageBack = Color.FromArgb(244, 246, 250);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(226, 232, 240);
    public static readonly Color Slate100 = Color.FromArgb(241, 245, 249);
    public static readonly Color Slate200 = Color.FromArgb(226, 232, 240);
    public static readonly Color Slate300 = Color.FromArgb(203, 213, 225);
    public static readonly Color Slate400 = Color.FromArgb(148, 163, 184);

    // Text
    public static readonly Color TextStrong = Color.FromArgb(15, 23, 42);
    public static readonly Color TextBody = Color.FromArgb(51, 65, 85);
    public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);

    // Brand
    public static readonly Color Primary = Color.FromArgb(37, 99, 235);
    public static readonly Color PrimaryHover = Color.FromArgb(29, 78, 216);
    public static readonly Color PrimaryDown = Color.FromArgb(30, 64, 175);
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);

    // Log terminal
    public static readonly Color LogBack = Color.FromArgb(15, 23, 42);
    public static readonly Color LogText = Color.FromArgb(226, 232, 240);
    public static readonly Color LogMuted = Color.FromArgb(100, 116, 139);
    public static readonly Color LogError = Color.FromArgb(248, 113, 113);
    public static readonly Color LogWarn = Color.FromArgb(251, 191, 36);

    public static readonly Color[] LogAppPalette =
    [
        Color.FromArgb(96, 165, 250), Color.FromArgb(52, 211, 153), Color.FromArgb(251, 191, 36),
        Color.FromArgb(167, 139, 250), Color.FromArgb(244, 114, 182), Color.FromArgb(45, 212, 191)
    ];

    // Fonts
    public static readonly Font Title = new(SansFamily, 18F, FontStyle.Bold);
    public static readonly Font CardTitle = new(SansFamily, 10.5F, FontStyle.Bold);
    public static readonly Font Body = new(SansFamily, 9.5F);
    public static readonly Font BodyBold = new(SansFamily, 9.5F, FontStyle.Bold);
    public static readonly Font Small = new(SansFamily, 9F);
    public static readonly Font Mono = new(MonoFamily, 9F);

    public static (Color Fore, Color Back, Color Accent) Colors(Tone tone) => tone switch
    {
        Tone.Success => (Color.FromArgb(21, 128, 61), Color.FromArgb(220, 252, 231), Color.FromArgb(34, 197, 94)),
        Tone.Warning => (Color.FromArgb(180, 83, 9), Color.FromArgb(254, 243, 199), Color.FromArgb(245, 158, 11)),
        Tone.Danger => (Color.FromArgb(185, 28, 28), Color.FromArgb(254, 226, 226), Color.FromArgb(239, 68, 68)),
        _ => (TextMuted, Slate100, Slate300)
    };

    public static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = Math.Max(2, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

#endregion

#region Panels, buttons, pills

/// <summary>Panel with rounded corners, a hairline border and an optional left accent bar.</summary>
internal class RoundedPanel : Panel
{
    private Color _accentColor = Color.Transparent;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 12;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor { get; set; } = Theme.Surface;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = Theme.Border;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor
    {
        get => _accentColor;
        set
        {
            if (_accentColor == value) return;
            _accentColor = value;
            Invalidate();
        }
    }

    public RoundedPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Theme.RoundedRect(rect, Radius))
        using (var fill = new SolidBrush(FillColor))
        using (var border = new Pen(BorderColor))
        {
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        if (_accentColor.A > 0 && Height > 32)
        {
            using var accentPath = Theme.RoundedRect(new Rectangle(10, 16, 4, Height - 33), 2);
            using var accent = new SolidBrush(_accentColor);
            g.FillPath(accent, accentPath);
        }
    }
}

internal enum ButtonKind { Secondary, Primary, Danger }

internal enum IconKind { None, Play, Stop, Clear, Refresh }

/// <summary>
/// Flat, rounded button with hover / pressed / disabled / focus states.
/// Shows text, an icon, or both (icon on the left).
/// </summary>
internal sealed class ModernButton : Button
{
    private const int IconSize = 14;
    private const int IconGap = 8;

    private bool _hover;
    private bool _down;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ButtonKind Kind { get; set; } = ButtonKind.Secondary;

    /// <summary>Colour of the surface behind the button, used to blend the rounded corners.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SurfaceColor { get; set; } = Theme.Surface;

    /// <summary>With an empty Text the button renders as a square icon-only button.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconKind Icon { get; set; } = IconKind.None;

    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Font = Theme.BodyBold;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = new Padding(0);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        bool hasIcon = Icon != IconKind.None;
        bool hasText = !string.IsNullOrEmpty(Text);
        if (hasIcon && !hasText) return new Size(36, 36);

        int textWidth = hasText ? TextRenderer.MeasureText(Text, Font).Width : 0;
        int width = textWidth + 32 + (hasIcon ? IconSize + IconGap : 0);
        return new Size(Math.Max(hasIcon ? 0 : 78, width), 36);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(SurfaceColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color back, fore, border;
        if (!Enabled)
        {
            back = Theme.Slate100;
            fore = Theme.Slate400;
            border = Theme.Slate200;
        }
        else
        {
            switch (Kind)
            {
                case ButtonKind.Primary:
                    back = _down ? Theme.PrimaryDown : _hover ? Theme.PrimaryHover : Theme.Primary;
                    fore = Color.White;
                    border = back;
                    break;
                case ButtonKind.Danger:
                    back = _down ? Color.FromArgb(254, 202, 202) : _hover ? Color.FromArgb(254, 226, 226) : Color.White;
                    fore = Theme.Danger;
                    border = Color.FromArgb(252, 165, 165);
                    break;
                default:
                    back = _down ? Theme.Slate200 : _hover ? Theme.Slate100 : Color.White;
                    fore = Theme.TextBody;
                    border = Theme.Slate300;
                    break;
            }
        }

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Theme.RoundedRect(rect, 8))
        using (var fill = new SolidBrush(back))
        using (var pen = new Pen(border))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        if (Focused && ShowFocusCues && Enabled)
        {
            var ring = new Rectangle(2, 2, Width - 5, Height - 5);
            using var ringPath = Theme.RoundedRect(ring, 6);
            using var ringPen = new Pen(Color.FromArgb(120, Theme.Primary), 1.5F);
            g.DrawPath(ringPen, ringPath);
        }

        bool hasIcon = Icon != IconKind.None;
        bool hasText = !string.IsNullOrEmpty(Text);

        if (hasIcon && !hasText)
        {
            DrawIcon(g, fore, Width / 2f, Height / 2f);
        }
        else if (hasIcon)
        {
            int textWidth = TextRenderer.MeasureText(Text, Font).Width;
            int total = IconSize + IconGap + textWidth;
            int x = Math.Max(8, (Width - total) / 2);
            DrawIcon(g, fore, x + IconSize / 2f, Height / 2f);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x + IconSize + IconGap, 0, Math.Max(0, Width - x - IconSize - IconGap), Height), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
        else
        {
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    private void DrawIcon(Graphics g, Color color, float cx, float cy)
    {
        using var brush = new SolidBrush(color);
        using var pen = new Pen(color, 1.6F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        switch (Icon)
        {
            case IconKind.Play:
                g.FillPolygon(brush, new[]
                {
                    new PointF(cx - 4f, cy - 6.5f),
                    new PointF(cx - 4f, cy + 6.5f),
                    new PointF(cx + 6.5f, cy)
                });
                break;

            case IconKind.Stop:
            {
                using var square = Theme.RoundedRect(new Rectangle((int)Math.Round(cx) - 6, (int)Math.Round(cy) - 6, 12, 12), 2);
                g.FillPath(brush, square);
                break;
            }

            case IconKind.Clear:
                // Trash can: lid + handle, body, two inner lines.
                g.DrawLine(pen, cx - 7f, cy - 4.5f, cx + 7f, cy - 4.5f);
                g.DrawLines(pen, new[]
                {
                    new PointF(cx - 2.5f, cy - 4.5f), new PointF(cx - 2.5f, cy - 7f),
                    new PointF(cx + 2.5f, cy - 7f), new PointF(cx + 2.5f, cy - 4.5f)
                });
                g.DrawLines(pen, new[]
                {
                    new PointF(cx - 5.5f, cy - 2f), new PointF(cx - 4.5f, cy + 7f),
                    new PointF(cx + 4.5f, cy + 7f), new PointF(cx + 5.5f, cy - 2f)
                });
                g.DrawLine(pen, cx - 2f, cy + 0.5f, cx - 2f, cy + 4.5f);
                g.DrawLine(pen, cx + 2f, cy + 0.5f, cx + 2f, cy + 4.5f);
                break;

            case IconKind.Refresh:
            {
                // Circular arrow: an open arc plus a small arrow head at its end.
                const float r = 5.5f;
                g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, 20f, 290f);
                double angle = 310 * Math.PI / 180;
                float nx = (float)Math.Cos(angle), ny = (float)Math.Sin(angle);
                float tx = -ny, ty = nx; // direction of travel (clockwise on screen)
                float ex = cx + r * nx, ey = cy + r * ny;
                g.FillPolygon(brush, new[]
                {
                    new PointF(ex + tx * 3.5f, ey + ty * 3.5f),
                    new PointF(ex + nx * 3.2f, ey + ny * 3.2f),
                    new PointF(ex - nx * 3.2f, ey - ny * 3.2f)
                });
                break;
            }
        }
    }
}

/// <summary>Small rounded "pill" showing a coloured dot and a status text.</summary>
internal sealed class StatusPill : Control
{
    private Tone _tone = Tone.Neutral;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Tone Tone
    {
        get => _tone;
        set
        {
            if (_tone == value) return;
            _tone = value;
            Invalidate();
        }
    }

    public StatusPill()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Theme.BodyBold;
        Size = new Size(116, 28);
        Margin = new Padding(0);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var (fore, back, _) = Theme.Colors(_tone);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Theme.RoundedRect(rect, Height / 2))
        using (var fill = new SolidBrush(back))
            g.FillPath(fill, path);

        const int dot = 8;
        using (var dotBrush = new SolidBrush(fore))
            g.FillEllipse(dotBrush, 12, (Height - dot) / 2, dot, dot);

        TextRenderer.DrawText(g, Text, Font, new Rectangle(26, 0, Math.Max(0, Width - 34), Height), fore,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}

#endregion

#region Navigation, toggle, brand

/// <summary>TabControl whose tab header is hidden; pair it with a <see cref="NavStrip"/>.</summary>
internal sealed class PagedTabControl : TabControl
{
    private const int TcmAdjustRect = 0x1328;

    protected override void WndProc(ref Message m)
    {
        // Make the pages fill the whole control so the native tab header is never visible.
        if (m.Msg == TcmAdjustRect && !DesignMode)
        {
            m.Result = (IntPtr)1;
            return;
        }
        base.WndProc(ref m);
    }
}

/// <summary>Modern underlined tab strip that drives a <see cref="TabControl"/>.</summary>
internal sealed class NavStrip : Control
{
    private const int PadX = 18;
    private readonly TabControl _tabs;
    private int _hover = -1;

    public NavStrip(TabControl tabs)
    {
        _tabs = tabs;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        TabStop = true;
        Font = Theme.BodyBold;
        BackColor = Theme.Surface;
        Height = 44;
        _tabs.SelectedIndexChanged += (_, _) => Invalidate();
    }

    private List<Rectangle> MeasureTabs()
    {
        var bounds = new List<Rectangle>();
        int x = 14;
        foreach (TabPage page in _tabs.TabPages)
        {
            int width = TextRenderer.MeasureText(page.Text, Font).Width + PadX * 2;
            bounds.Add(new Rectangle(x, 0, width, Height));
            x += width + 4;
        }
        return bounds;
    }

    private int HitTest(Point point)
    {
        var bounds = MeasureTabs();
        for (int i = 0; i < bounds.Count; i++)
            if (bounds[i].Contains(point)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hover = HitTest(e.Location);
        if (hover == _hover) return;
        _hover = hover;
        Cursor = hover >= 0 ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int index = HitTest(e.Location);
        if (index >= 0)
        {
            _tabs.SelectedIndex = index;
            Focus();
        }
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int count = _tabs.TabPages.Count;
        if (count == 0) return;
        if (e.KeyCode == Keys.Right) _tabs.SelectedIndex = Math.Min(count - 1, _tabs.SelectedIndex + 1);
        else if (e.KeyCode == Keys.Left) _tabs.SelectedIndex = Math.Max(0, _tabs.SelectedIndex - 1);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var borderPen = new Pen(Theme.Border))
            g.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);

        var bounds = MeasureTabs();
        for (int i = 0; i < bounds.Count; i++)
        {
            var rect = bounds[i];
            bool selected = i == _tabs.SelectedIndex;
            var color = selected ? Theme.Primary : i == _hover ? Theme.TextStrong : Theme.TextMuted;

            TextRenderer.DrawText(g, _tabs.TabPages[i].Text, Font,
                new Rectangle(rect.X, 0, rect.Width, Height - 3), color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

            if (selected)
            {
                using var path = Theme.RoundedRect(new Rectangle(rect.X + PadX - 4, Height - 3, rect.Width - PadX * 2 + 8, 3), 1);
                using var brush = new SolidBrush(Theme.Primary);
                g.FillPath(brush, path);

                if (Focused && ShowFocusCues)
                {
                    using var focusPen = new Pen(Color.FromArgb(90, Theme.Primary)) { DashStyle = DashStyle.Dot };
                    g.DrawRectangle(focusPen, rect.X + 4, 6, rect.Width - 9, Height - 14);
                }
            }
        }
    }
}

/// <summary>Switch-style CheckBox (pill track with a sliding knob).</summary>
internal sealed class ToggleSwitch : CheckBox
{
    private const int TrackWidth = 38;
    private const int TrackHeight = 22;
    private const int Gap = 10;
    private bool _hover;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SurfaceColor { get; set; } = Theme.PageBack;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        Cursor = Cursors.Hand;
        Font = Theme.Body;
        ForeColor = Theme.TextBody;
        AutoSize = true;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int textWidth = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(Text, Font).Width;
        return new Size(TrackWidth + (textWidth > 0 ? Gap + textWidth : 0) + 4, 32);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(SurfaceColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int y = (Height - TrackHeight) / 2;
        var track = new Rectangle(1, y, TrackWidth - 1, TrackHeight - 1);

        Color trackColor = !Enabled ? Theme.Slate200
            : Checked ? (_hover ? Theme.PrimaryHover : Theme.Primary)
            : (_hover ? Theme.Slate400 : Theme.Slate300);

        using (var path = Theme.RoundedRect(track, TrackHeight / 2))
        using (var brush = new SolidBrush(trackColor))
            g.FillPath(brush, path);

        const int knob = 16;
        int knobX = Checked ? track.Right - 3 - knob : track.X + 3;
        using (var knobBrush = new SolidBrush(Color.White))
            g.FillEllipse(knobBrush, knobX, y + 3, knob, knob);

        if (Focused && ShowFocusCues && Enabled)
        {
            var ring = Rectangle.Inflate(track, 2, 2);
            using var ringPath = Theme.RoundedRect(ring, ring.Height / 2);
            using var ringPen = new Pen(Color.FromArgb(120, Theme.Primary), 1.5F);
            g.DrawPath(ringPen, ringPath);
        }

        if (!string.IsNullOrEmpty(Text))
        {
            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(TrackWidth + Gap, 0, Math.Max(0, Width - TrackWidth - Gap), Height),
                Enabled ? ForeColor : Theme.Slate400,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }
}

/// <summary>Rounded gradient app mark with a tiny server-rack glyph.</summary>
internal sealed class BrandMark : Control
{
    public BrandMark()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(40, 40);
        Margin = new Padding(0);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Theme.RoundedRect(rect, 11))
        using (var gradient = new LinearGradientBrush(rect, Color.FromArgb(59, 130, 246), Color.FromArgb(79, 70, 229), 45F))
            g.FillPath(gradient, path);

        float s = Width / 40f;
        using var barBrush = new SolidBrush(Color.FromArgb(240, 255, 255, 255));
        using var dotBrush = new SolidBrush(Color.FromArgb(79, 70, 229));
        foreach (float top in new[] { 10f, 22f })
        {
            var bar = new Rectangle((int)(9 * s), (int)(top * s), (int)(22 * s), (int)(8 * s));
            using (var barPath = Theme.RoundedRect(bar, (int)(3 * s)))
                g.FillPath(barBrush, barPath);
            float d = 3.5f * s;
            g.FillEllipse(dotBrush, bar.Right - 4.5f * s - d / 2f, bar.Y + bar.Height / 2f - d / 2f, d, d);
        }
    }
}

#endregion