using System.Runtime.InteropServices;
using JukeBot.Core.Models;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;

namespace JukeBot.Core.Audio;

public class LibVlcAudioBackend : IAudioBackend
{
    private readonly ILogger<LibVlcAudioBackend> _logger;
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;
    private List<Track> _queue = new();
    private int _currentIndex = -1;
    private Track? _currentTrack;
    private bool _disposed;
    private readonly System.Timers.Timer _positionTimer;

    public event EventHandler<TrackChangedEventArgs>? TrackChanged;
    public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsPlaying => _mediaPlayer.State == VLCState.Playing;
    public bool IsPaused => _mediaPlayer.State == VLCState.Paused;
    public TimeSpan CurrentTime => TimeSpan.FromMilliseconds(_mediaPlayer.Time);
    public TimeSpan Duration => TimeSpan.FromMilliseconds(_mediaPlayer.Length);
    public Track? CurrentTrack => _currentTrack;

    private int _volume = 50;
    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            _mediaPlayer.Volume = _volume;
        }
    }

    private bool _muted;
    public bool Muted
    {
        get => _muted;
        set
        {
            _muted = value;
            _mediaPlayer.Mute = value;
        }
    }

    public LibVlcAudioBackend(ILogger<LibVlcAudioBackend> logger)
    {
        _logger = logger;

        // Initialize LibVLC with high-quality audio settings
        _libVlc = new LibVLC(
            "--no-video",                          // Disable video
            "--quiet",                             // Quiet mode
            "--audio-resampler=soxr",             // High-quality SoX resampler
            "--soxr-resampler-quality=10",        // Maximum quality (0-10, default 3)
            "--audio-filter=",                    // Disable audio filters
            "--audio-visual=",                    // Disable visualizer processing
            "--gain=1.0",                         // Unity gain
            "--volume-save",                      // Remember volume
            "--audio-desync=0"                    // No audio desync
        );
        _mediaPlayer = new MediaPlayer(_libVlc);

        _mediaPlayer.Playing += OnPlaying;
        _mediaPlayer.Paused += OnPaused;
        _mediaPlayer.Stopped += OnStopped;
        _mediaPlayer.EndReached += OnEndReached;
        _mediaPlayer.EncounteredError += OnError;

        _positionTimer = new System.Timers.Timer(100);
        _positionTimer.Elapsed += (s, e) =>
        {
            if (IsPlaying)
                PositionChanged?.Invoke(this, CurrentTime);
        };
        _positionTimer.Start();

        // Set up audio callbacks for analysis
        SetupAudioCallbacks();
    }

    private void SetupAudioCallbacks()
    {
        try
        {
            // LibVLC audio callbacks are complex and platform-specific
            // Simplified implementation: skip callbacks for now
            // Visualizer will use fallback RMS-based metering
            _logger.LogInformation("Audio callbacks not implemented in this version");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to setup audio callbacks for analysis");
        }
    }

    public void LoadQueue(List<Track> tracks, int startIndex = 0)
    {
        if (tracks == null || tracks.Count == 0)
            throw new ArgumentException("Queue cannot be empty", nameof(tracks));

        _queue = new List<Track>(tracks);
        _currentIndex = Math.Clamp(startIndex, 0, tracks.Count - 1);

        LoadTrack(_currentIndex);
    }

    private void LoadTrack(int index)
    {
        if (index < 0 || index >= _queue.Count)
        {
            _logger.LogWarning("Invalid queue index: {Index}", index);
            return;
        }

        _currentIndex = index;
        _currentTrack = _queue[index];

        try
        {
            var media = new Media(_libVlc, _currentTrack.FilePath, FromType.FromPath);
            _mediaPlayer.Media = media;

            TrackChanged?.Invoke(this, new TrackChangedEventArgs
            {
                Track = _currentTrack,
                QueueIndex = _currentIndex
            });

            _logger.LogInformation("Loaded track: {Title} by {Artist}",
                _currentTrack.Title ?? _currentTrack.FilePath,
                _currentTrack.Artist ?? "Unknown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load track: {Path}", _currentTrack.FilePath);
            ErrorOccurred?.Invoke(this, $"Failed to load track: {ex.Message}");
        }
    }

    public async Task PlayAsync()
    {
        if (_mediaPlayer.Media == null && _queue.Count > 0)
        {
            LoadTrack(_currentIndex >= 0 ? _currentIndex : 0);
        }

        if (_mediaPlayer.Media != null)
        {
            await Task.Run(() => _mediaPlayer.Play());
        }
    }

    public void Pause()
    {
        if (IsPlaying)
        {
            _mediaPlayer.Pause();
        }
        else if (IsPaused)
        {
            _mediaPlayer.Play();
        }
    }

    public void Stop()
    {
        _mediaPlayer.Stop();
    }

    public void Next()
    {
        if (_queue.Count == 0) return;

        var nextIndex = (_currentIndex + 1) % _queue.Count;
        LoadTrack(nextIndex);
        _ = PlayAsync();
    }

    public void Previous()
    {
        if (_queue.Count == 0) return;

        // If more than 3 seconds in, restart current track
        if (CurrentTime.TotalSeconds > 3)
        {
            Seek(TimeSpan.Zero);
            return;
        }

        var prevIndex = _currentIndex - 1;
        if (prevIndex < 0) prevIndex = _queue.Count - 1;

        LoadTrack(prevIndex);
        _ = PlayAsync();
    }

    public void Seek(TimeSpan position)
    {
        if (_mediaPlayer.IsSeekable)
        {
            _mediaPlayer.Time = (long)position.TotalMilliseconds;
        }
    }

    public void SeekRelative(TimeSpan offset)
    {
        var newPosition = CurrentTime + offset;
        if (newPosition < TimeSpan.Zero) newPosition = TimeSpan.Zero;
        if (newPosition > Duration) newPosition = Duration;
        Seek(newPosition);
    }

    private void OnPlaying(object? sender, EventArgs e)
    {
        PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
        {
            State = PlaybackState.Playing
        });
    }

    private void OnPaused(object? sender, EventArgs e)
    {
        PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
        {
            State = PlaybackState.Paused
        });
    }

    private void OnStopped(object? sender, EventArgs e)
    {
        PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
        {
            State = PlaybackState.Stopped
        });
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        _logger.LogInformation("Track ended, moving to next");
        Next();
    }

    private void OnError(object? sender, EventArgs e)
    {
        _logger.LogError("Playback error occurred");
        ErrorOccurred?.Invoke(this, "Playback error occurred");
        PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
        {
            State = PlaybackState.Error
        });
    }

    public void Dispose()
    {
        if (_disposed) return;

        _positionTimer?.Stop();
        _positionTimer?.Dispose();
        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _libVlc?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
