namespace VideoWallpaper;

internal sealed class SettingsForm : Form
{
    readonly ComboBox _kind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    readonly TextBox _source = new() { Width = 300 };
    readonly Button _browse = new() { Text = "Browse...", AutoSize = true };
    readonly CheckBox _shuffle = new() { Text = "Shuffle playlist", AutoSize = true };
    readonly CheckBox _mute = new() { Text = "Mute", AutoSize = true };
    readonly TrackBar _volume = new() { Minimum = 0, Maximum = 100, TickFrequency = 25, Width = 300 };
    readonly ComboBox _monitor = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    readonly CheckBox _pauseFs = new() { Text = "Pause when a fullscreen app is active", AutoSize = true };
    readonly CheckBox _autostart = new() { Text = "Start with Windows", AutoSize = true };

    readonly Settings _s;
    readonly Action<Settings> _apply;

    public SettingsForm(Settings s, Action<Settings> apply)
    {
        _s = s;
        _apply = apply;

        Text = "Video Wallpaper Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        _kind.Items.AddRange(new object[] { "Local video (loops)", "YouTube video", "YouTube playlist" });
        for (int i = 0; i < Screen.AllScreens.Length; i++)
        {
            var b = Screen.AllScreens[i].Bounds;
            _monitor.Items.Add($"Monitor {i + 1} ({b.Width}x{b.Height})");
        }

        var ok = new Button { Text = "Apply", AutoSize = true };
        var cancel = new Button { Text = "Close", AutoSize = true };
        cancel.Click += (_, _) => Close();
        AcceptButton = ok;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange(new Control[] { ok, cancel });
        var srcRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        srcRow.Controls.AddRange(new Control[] { _source, _browse });

        var panel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
        panel.Controls.AddRange(new Control[]
        {
            new Label { Text = "Source", AutoSize = true }, _kind,
            new Label { Text = "File path or URL", AutoSize = true }, srcRow,
            _shuffle, _mute,
            new Label { Text = "Volume", AutoSize = true }, _volume,
            new Label { Text = "Monitor", AutoSize = true }, _monitor,
            _pauseFs, _autostart, buttons,
        });
        Controls.Add(panel);

        _kind.SelectedIndex = (int)s.Kind;
        _source.Text = s.Source;
        _shuffle.Checked = s.Shuffle;
        _mute.Checked = s.Mute;
        _volume.Value = Math.Clamp(s.Volume, 0, 100);
        _monitor.SelectedIndex = Math.Clamp(s.MonitorIndex, 0, _monitor.Items.Count - 1);
        _pauseFs.Checked = s.PauseWhenFullscreenApp;
        _autostart.Checked = Settings.GetAutostart();

        _kind.SelectedIndexChanged += (_, _) => UpdateUi();
        _mute.CheckedChanged += (_, _) => _volume.Enabled = !_mute.Checked;
        _browse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Video files|*.mp4;*.mkv;*.webm;*.avi;*.mov;*.wmv;*.m4v;*.gif|All files|*.*"
            };
            if (dlg.ShowDialog(this) == DialogResult.OK) _source.Text = dlg.FileName;
        };
        ok.Click += (_, _) => { Commit(); Close(); };
        UpdateUi();
    }

    void UpdateUi()
    {
        _browse.Visible = _kind.SelectedIndex == (int)SourceKind.LocalFile;
        _shuffle.Enabled = _kind.SelectedIndex == (int)SourceKind.YouTubePlaylist;
        _volume.Enabled = !_mute.Checked;
    }

    void Commit()
    {
        _s.Kind = (SourceKind)_kind.SelectedIndex;
        _s.Source = _source.Text.Trim().Trim('"');
        _s.Shuffle = _shuffle.Checked;
        _s.Mute = _mute.Checked;
        _s.Volume = _volume.Value;
        _s.MonitorIndex = Math.Max(0, _monitor.SelectedIndex);
        _s.PauseWhenFullscreenApp = _pauseFs.Checked;
        Settings.SetAutostart(_autostart.Checked);
        _s.Save();
        _apply(_s);
    }
}
