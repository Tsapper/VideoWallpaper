using System.Text.Json;
using Microsoft.Win32;

namespace VideoWallpaper;

public enum SourceKind { LocalFile, YouTubeVideo, YouTubePlaylist }

public sealed class Settings
{
    public SourceKind Kind { get; set; } = SourceKind.LocalFile;
    public string Source { get; set; } = "";
    public bool Shuffle { get; set; } = true;
    public bool Mute { get; set; } = true;
    public int Volume { get; set; } = 50;
    public int MonitorIndex { get; set; } = 0;
    public bool PauseWhenFullscreenApp { get; set; } = true;

    static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoWallpaper");
    static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public static string DataDir
    {
        get { Directory.CreateDirectory(Dir); return Dir; }
    }

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { }
        return new Settings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool GetAutostart()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue("VideoWallpaper") != null;
    }

    public static void SetAutostart(bool on)
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (k == null) return;
        if (on) k.SetValue("VideoWallpaper", $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue("VideoWallpaper", false);
    }
}
