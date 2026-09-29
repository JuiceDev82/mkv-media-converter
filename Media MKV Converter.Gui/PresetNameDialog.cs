namespace Media_MKV_Converter.Gui;

/// <summary>Minimal "type a name" prompt — WinForms has no built-in input box. Used for naming a new
/// or renamed preset.</summary>
internal sealed class PresetNameDialog : Form
{
    private readonly TextBox _textBox;

    public string PresetName => _textBox.Text.Trim();

    public PresetNameDialog(string title, string initialValue)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(360, 110);

        var label = new Label
        {
            Text = "Preset name:",
            AutoSize = true,
            Location = new Point(12, 15),
        };

        _textBox = new TextBox
        {
            Text = initialValue,
            Location = new Point(12, 36),
            Size = new Size(336, 23),
        };
        _textBox.SelectAll();

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(192, 72),
            Size = new Size(75, 25),
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(273, 72),
            Size = new Size(75, 25),
        };

        Controls.Add(label);
        Controls.Add(_textBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
