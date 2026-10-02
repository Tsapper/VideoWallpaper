using System.Runtime.InteropServices;

namespace VideoWallpaper;

/// <summary>Finds the desktop host window that sits behind the icons and reparents a form into it.</summary>
internal static class DesktopHost
{
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string? cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child, IntPtr newParent);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr hWnd, ref POINT pt);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int idx);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int idx, IntPtr value);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

    const int GWL_STYLE = -16;
    const long WS_CHILD = 0x40000000, WS_POPUP = 0x80000000, WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000;
    const uint SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040, SWP_NOZORDER = 0x0004;

    public static IntPtr FindHost()
    {
        var progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return IntPtr.Zero;

        // Ask Progman to spawn the WorkerW that sits behind the icons.
        SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(1), 0, 1000, out _);

        // Windows 11 24H2+: WorkerW is a child of Progman.
        var child = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        if (child != IntPtr.Zero) return child;

        // Older layout: the WorkerW *after* the one that holds SHELLDLL_DefView (top-level sibling).
        IntPtr workerW = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                workerW = FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
            return true;
        }, IntPtr.Zero);
        // Last resort: Progman itself (video draws above the wallpaper, below nothing else on old builds).
        return workerW != IntPtr.Zero ? workerW : progman;
    }

    public static bool IsAlive(IntPtr host) => host != IntPtr.Zero && IsWindow(host);

    /// <summary>Reparent <paramref name="hwnd"/> into the host and cover <paramref name="screenBounds"/>.</summary>
    public static bool Attach(IntPtr hwnd, IntPtr host, Rectangle screenBounds)
    {
        if (host == IntPtr.Zero) return false;

        long style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
        style = (style & ~(WS_POPUP | WS_CAPTION | WS_THICKFRAME)) | WS_CHILD;
        SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(style));
        SetParent(hwnd, host);
        return Resize(hwnd, host, screenBounds);
    }

    public static bool Resize(IntPtr hwnd, IntPtr host, Rectangle screenBounds)
    {
        var pt = new POINT { X = screenBounds.X, Y = screenBounds.Y };
        ScreenToClient(host, ref pt);
        return SetWindowPos(hwnd, IntPtr.Zero, pt.X, pt.Y, screenBounds.Width, screenBounds.Height,
            SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOZORDER);
    }
}
