using LibVLCSharp.Shared;

namespace VideoWallpaper;

/// <summary>Owns LibVLC and drives playback of a local loop, a YouTube video, or a shuffled playlist.</summary>
internal sealed class PlayerService : IDisposable
{
    readonly LibVLC _vlc;
    readonly MediaPlayer _player;
    readonly YtDlpService _yt = new();
    CancellationTokenSource _cts = new();
    List<string> _queue = new();
    int _index;
    Settings _settings = new();
    int _maxHeight = 1080;
    Size _screen;
    int _failures;

    public MediaPlayer Player => _player;
    public event Action<string>? Status;

    public PlayerService()
    {
        Core.Initialize();
        _vlc = new LibVLC(
            "--no-osd", "--no-video-title-show", "--no-snapshot-preview", "--no-stats",
            "--no-sub-autodetect-file", "--no-spu", "--quiet",
            // Don't hold a display/system wake request, so Windows can sleep normally.
            "--no-disable-screensaver",
            "--avcodec-hw=any", "--avcodec-threads=2",
            "--file-caching=150", "--network-caching=500", "--live-caching=300",
            "--prefetch-buffer-size=1024", "--prefetch-read-size=65536",
            "--drop-late-frames", "--skip-frames");
        _player = new MediaPlayer(_vlc) { EnableHardwareDecoding = true, EnableMouseInput = false, EnableKeyInput = false };
        _player.EndReached += (_, _) => Task.Run(OnEnded);
        _player.EncounteredError += (_, _) => Task.Run(OnError);
        _player.Playing += (_, _) => ApplyCrop();
    }

    // Crop the video to the screen's aspect ratio so it covers the whole window (no bars) instead of letterboxing.
    void ApplyCrop()
    {
        if (_screen.Width > 0 && _screen.Height > 0)
            _player.CropGeometry = $"{_screen.Width}:{_screen.Height}";
    }

    public void Start(Settings s, Size screen)
    {
        _settings = s;
        _screen = screen;
        _maxHeight = screen.Height;
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _failures = 0;
        ApplyAudio();
        Task.Run(() => StartAsync(ct));
    }

    public void ApplyAudio()
    {
        _player.Mute = _settings.Mute;
        _player.Volume = Math.Clamp(_settings.Volume, 0, 100);
    }

    async Task StartAsync(CancellationToken ct)
    {
        try
        {
            switch (_settings.Kind)
            {
                case SourceKind.LocalFile:
                    if (!File.Exists(_settings.Source)) { Status?.Invoke("Choose a video file in Settings."); return; }
                    PlayMedia(new[] { _settings.Source }, local: true);
                    break;

                case SourceKind.YouTubeVideo:
                    if (string.IsNullOrWhiteSpace(_settings.Source)) { Status?.Invoke("Enter a YouTube URL in Settings."); return; }
                    _queue = new() { _settings.Source };
                    _index = 0;
                    await PlayCurrentAsync(ct);
                    break;

                case SourceKind.YouTubePlaylist:
                    if (string.IsNullOrWhiteSpace(_settings.Source)) { Status?.Invoke("Enter a playlist URL in Settings."); return; }
                    Status?.Invoke("Loading playlist...");
                    _queue = await _yt.GetPlaylistAsync(_settings.Source, ct);
                    if (_queue.Count == 0) { Status?.Invoke("Playlist is empty or unavailable."); return; }
                    if (_settings.Shuffle) Shuffle(_queue);
                    _index = 0;
                    await PlayCurrentAsync(ct);
                    break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Status?.Invoke(ex.Message); }
    }

    async Task PlayCurrentAsync(CancellationToken ct)
    {
        var urls = await _yt.ResolveAsync(_queue[_index], _maxHeight, ct);
        ct.ThrowIfCancellationRequested();
        PlayMedia(urls, local: false);
    }

    void PlayMedia(string[] urls, bool local)
    {
        using var media = local
            ? new Media(_vlc, urls[0], FromType.FromPath)
            : new Media(_vlc, urls[0], FromType.FromLocation);
        if (local) media.AddOption(":input-repeat=65535");
        if (urls.Length > 1) media.AddOption(":input-slave=" + urls[1]);
        _player.Play(media);
        _failures = 0;
    }

    void OnEnded()
    {
        var ct = _cts.Token;
        if (_settings.Kind == SourceKind.LocalFile) { _player.Play(); return; }
        _ = AdvanceAsync(ct, 1);
    }

    void OnError()
    {
        var ct = _cts.Token;
        if (++_failures > 3) { Status?.Invoke("Playback failed repeatedly."); return; }
        // Stream URLs expire; re-resolve the current entry (or skip on repeated failure).
        _ = AdvanceAsync(ct, _failures > 1 ? 1 : 0);
    }

    async Task AdvanceAsync(CancellationToken ct, int step)
    {
        try
        {
            if (_queue.Count == 0) return;
            _index += step;
            if (_index >= _queue.Count)
            {
                _index = 0;
                if (_settings.Kind == SourceKind.YouTubePlaylist && _settings.Shuffle) Shuffle(_queue);
            }
            await PlayCurrentAsync(ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Status?.Invoke(ex.Message); }
    }

    /// <summary>Restart the current item after system resume (stream URLs expire and the network may have dropped).</summary>
    public void Restart()
    {
        var ct = _cts.Token;
        _failures = 0;
        if (_settings.Kind == SourceKind.LocalFile) { Task.Run(() => { _player.Stop(); _player.Play(); }); return; }
        _ = Task.Run(async () =>
        {
            await Task.Delay(5000, ct).ConfigureAwait(false); // let Wi-Fi reconnect
            await AdvanceAsync(ct, 0);
        });
    }

    public void Next()
    {
        if (_settings.Kind != SourceKind.YouTubePlaylist) return;
        var ct = _cts.Token;
        _ = Task.Run(() => AdvanceAsync(ct, 1));
    }

    public void SetPaused(bool paused)
    {
        if (paused) { if (_player.IsPlaying) _player.SetPause(true); }
        else if (_player.State == VLCState.Paused) _player.SetPause(false);
    }

    public bool IsPaused => _player.State == VLCState.Paused;

    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _player.Stop();
        _player.Dispose();
        _vlc.Dispose();
    }
}
