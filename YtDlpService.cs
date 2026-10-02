using System.Diagnostics;

namespace VideoWallpaper;

/// <summary>Wraps yt-dlp.exe: downloads it on demand, resolves direct stream URLs and expands playlists.</summary>
internal sealed class YtDlpService
{
    const string DownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    static readonly string ExePath = Path.Combine(Settings.DataDir, "yt-dlp.exe");
    readonly SemaphoreSlim _gate = new(1, 1);
    bool _updatedThisRun;

    public async Task EnsureAsync(CancellationToken ct)
    {
        if (File.Exists(ExePath)) return;
        await _gate.WaitAsync(ct);
        try
        {
            if (File.Exists(ExePath)) return;
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var tmp = ExePath + ".tmp";
            await using (var s = await http.GetStreamAsync(DownloadUrl, ct))
            await using (var f = File.Create(tmp))
                await s.CopyToAsync(f, ct);
            File.Move(tmp, ExePath, true);
        }
        finally { _gate.Release(); }
    }

    async Task<(int code, string stdout, string stderr)> RunAsync(string args, CancellationToken ct)
    {
        await EnsureAsync(ct);
        var psi = new ProcessStartInfo(ExePath, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
        };
        using var p = Process.Start(psi)!;
        var outTask = p.StandardOutput.ReadToEndAsync(ct);
        var errTask = p.StandardError.ReadToEndAsync(ct);
        try { await p.WaitForExitAsync(ct); }
        catch { try { p.Kill(true); } catch { } throw; }
        return (p.ExitCode, await outTask, await errTask);
    }

    /// <summary>Run with one retry after self-updating yt-dlp (YouTube changes break old versions).</summary>
    async Task<string> RunWithUpdateAsync(string args, CancellationToken ct)
    {
        var (code, so, se) = await RunAsync(args, ct);
        if (code != 0 && !_updatedThisRun)
        {
            _updatedThisRun = true;
            await RunAsync("-U", ct);
            (code, so, se) = await RunAsync(args, ct);
        }
        if (code != 0) throw new InvalidOperationException("yt-dlp failed: " + se.Trim());
        return so;
    }

    /// <summary>Returns [video] or [video, audio] direct URLs capped to the screen height and 30 fps.</summary>
    public async Task<string[]> ResolveAsync(string videoUrl, int maxHeight, CancellationToken ct)
    {
        // Resolution wins; among equal resolutions prefer <=30 fps, then H.264 (cheapest to decode). AV1 is skipped.
        var fmt = $"bv*[height<={maxHeight}][vcodec!^=av01]+ba/b[height<={maxHeight}]/b";
        var so = await RunWithUpdateAsync($"-g --no-playlist --no-warnings -f \"{fmt}\" -S \"res:{maxHeight},fps:30,vcodec:h264\" \"{videoUrl}\"", ct);
        return so.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task<List<string>> GetPlaylistAsync(string playlistUrl, CancellationToken ct)
    {
        var so = await RunWithUpdateAsync($"--flat-playlist --no-warnings --print url \"{playlistUrl}\"", ct);
        return so.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Where(u => u.StartsWith("http", StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
