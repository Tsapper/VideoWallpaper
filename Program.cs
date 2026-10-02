namespace VideoWallpaper;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "VideoWallpaper.SingleInstance", out bool first);
        if (!first) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
