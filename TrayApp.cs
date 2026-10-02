using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Text;

namespace VideoWallpaper;

internal sealed class TrayApp : ApplicationContext
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("psapi.dll")] static extern bool EmptyWorkingSet(IntPtr process);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    int _ticks;

    readonly NotifyIcon _tray;
    readonly WallpaperWindow _window = new();
    readonly PlayerService _player = new();
    readonly System.Windows.Forms.Timer _fsTimer = new() { Interval = 2000 };
    readonly Settings _settings = Settings.Load();
    readonly ToolStripMenuItem _pauseItem = new("Pause");
    SettingsForm? _form;
    bool _userPaused, _autoPaused;

    public TrayApp()
    {
        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Video Wallpaper",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _tray.DoubleClick += (_, _) => ShowSettings();
        _player.Status += msg => _window.BeginInvoke(() => _tray.ShowBalloonTip(3000, "Video Wallpaper", msg, ToolTipIcon.Info));

        _window.ShowOn(_settings.MonitorIndex);          // creates the handle before the player attaches
        _window.View.MediaPlayer = _player.Player;
        _fsTimer.Tick += (_, _) =>
        {
            CheckFullscreen();
            if (++_ticks % 15 == 0) TrimMemory();
        };
        _fsTimer.Start();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        if (string.IsNullOrWhiteSpace(_settings.Source)) ShowSettings();
        else ApplySettings(_settings);
    }

    void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) _window.BeginInvoke(() => _player.SetPaused(true));
        else if (e.Mode == PowerModes.Resume)
            _window.BeginInvoke(() =>
            {
                DesktopHost.Resize(_window.Handle, DesktopHost.FindHost(), _window.TargetBounds);
                _player.Restart();
            });
    }

    // Release pages that aren't actively used so the working set stays small.
    void TrimMemory()
    {
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        EmptyWorkingSet(Process.GetCurrentProcess().Handle);
    }

    ContextMenuStrip BuildMenu()
    {
        var m = new ContextMenuStrip();
        m.Items.Add("Settings...", null, (_, _) => ShowSettings());
        _pauseItem.Click += (_, _) => { _userPaused = !_userPaused; UpdatePause(); };
        m.Items.Add(_pauseItem);
        m.Items.Add("Next video", null, (_, _) => _player.Next());
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Exit", null, (_, _) => ExitThread());
        return m;
    }

    void ShowSettings()
    {
        if (_form is { IsDisposed: false }) { _form.Activate(); return; }
        _form = new SettingsForm(_settings, ApplySettings);
        _form.FormClosed += (_, _) => { _form.Dispose(); _form = null; };
        _form.Show();
    }

    void ApplySettings(Settings s)
    {
        _window.ShowOn(s.MonitorIndex);
        _player.Start(s, _window.TargetBounds.Size);
    }

    void UpdatePause()
    {
        bool pause = _userPaused || _autoPaused;
        _player.SetPaused(pause);
        _pauseItem.Text = _userPaused ? "Resume" : "Pause";
    }

    void CheckFullscreen()
    {
        bool full = _settings.PauseWhenFullscreenApp && IsFullscreenAppActive();
        if (full == _autoPaused) return;
        _autoPaused = full;
        UpdatePause();
    }

    bool IsFullscreenAppActive()
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == _window.Handle) return false;
        var sb = new StringBuilder(64);
        GetClassName(fg, sb, sb.Capacity);
        var cls = sb.ToString();
        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        if (!GetWindowRect(fg, out var r)) return false;
        var b = _window.TargetBounds;
        return r.L <= b.Left && r.T <= b.Top && r.R >= b.Right && r.B >= b.Bottom;
    }

    protected override void ExitThreadCore()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _fsTimer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        _window.View.MediaPlayer = null;
        _player.Dispose();
        _window.Close();
        base.ExitThreadCore();
    }
}
