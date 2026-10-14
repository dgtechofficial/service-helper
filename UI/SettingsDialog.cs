namespace PostgresRedisManager.UI;

internal sealed class SettingsDialog : Form
{
    private readonly List<TextBox> _inputs = new();
    public string[] Values => _inputs.Select(input => input.Text.Trim()).ToArray();

    public SettingsDialog(string title, (string Label, string Value)[] fields)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(660, 84 + fields.Length * 58);
        Font = new Font("Segoe UI", 9.5F);

        for (int index = 0; index < fields.Length; index++)
        {
            int top = 16 + index * 58;
            Controls.Add(new Label
            {
                Text = fields[index].Label,
                AutoSize = true,
                Location = new Point(18, top + 5)
            });
            var input = new TextBox
            {
                Text = fields[index].Value,
                Location = new Point(205, top),
                Width = 430,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(input);
            _inputs.Add(input);
        }

        var save = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(ClientSize.Width - 205, ClientSize.Height - 50),
            Width = 86
        };
        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(ClientSize.Width - 108, ClientSize.Height - 50),
            Width = 90
        };
        Controls.Add(save);
        Controls.Add(cancel);
        AcceptButton = save;
        CancelButton = cancel;
    }
}
