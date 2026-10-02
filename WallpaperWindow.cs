using System.Runtime.InteropServices;
using LibVLCSharp.WinForms;
using Microsoft.Win32;

namespace VideoWallpaper;

/// <summary>Borderless window hosting the video; reparented behind the desktop icons.</summary>
internal sealed class WallpaperWindow : Form
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);

    readonly VideoView _view = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    readonly uint _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    IntPtr _host;
    int _monitorIndex;

    public VideoView View => _view;

    public Rectangle TargetBounds => ScreenAt(_monitorIndex).Bounds;

    public WallpaperWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        Controls.Add(_view);
        SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80 | 0x08000000; // WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    static Screen ScreenAt(int i) => Screen.AllScreens[Math.Clamp(i, 0, Screen.AllScreens.Length - 1)];

    public void ShowOn(int monitorIndex)
    {
        _monitorIndex = monitorIndex;
        var b = TargetBounds;
        Bounds = b;
        if (!Visible) Show();
        Attach();
    }

    void Attach()
    {
        _host = DesktopHost.FindHost();
        DesktopHost.Attach(Handle, _host, TargetBounds);
    }

    void OnDisplayChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            if (!DesktopHost.IsAlive(_host)) _host = DesktopHost.FindHost();
            DesktopHost.Resize(Handle, _host, TargetBounds);
        });
    }

    protected override void WndProc(ref Message m)
    {
        // Explorer restarted: its desktop windows are new, so reattach.
        if (m.Msg == _taskbarCreated) BeginInvoke(Attach);
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        base.Dispose(disposing);
    }
}
