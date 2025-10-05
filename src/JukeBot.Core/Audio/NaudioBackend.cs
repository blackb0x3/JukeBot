using JukeBot.Core.Models;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace JukeBot.Core.Audio;

public class NaudioBackend : IAudioBackend
{
    private readonly ILogger<NaudioBackend> _logger;
    private IWavePlayer? _wavePlayer;
    private AudioFileReader? _audioFileReader;
    private List<Track> _queue = new();
    private int _currentIndex = -1;
    private Track? _currentTrack;
    private bool _disposed;
    private readonly System.Timers.Timer _positionTimer;
    private bool _isPlaying;
    private bool _isPaused;
    private VolumeSampleProvider? _volumeProvider;
    private MeteringSampleProvider? _meteringProvider;

    public event EventHandler<TrackChangedEventArgs>? TrackChanged;
    public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsPlaying => _isPlaying;
    public bool IsPaused => _isPaused;
    public TimeSpan CurrentTime => _audioFileReader?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan Duration => _audioFileReader?.TotalTime ?? TimeSpan.Zero;
    public Track? CurrentTrack => _currentTrack;

    private int _volume = 50;
    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            if (_volumeProvider != null)
            {
                _volumeProvider.Volume = _volume / 100f;
            }
        }
    }

    private bool _muted;
    public bool Muted
    {
        get => _muted;
        set
        {
            _muted = value;
            if (_volumeProvider != null)
            {
                _volumeProvider.Volume = _muted ? 0 : _volume / 100f;
            }
        }
    }

    public NaudioBackend(ILogger<NaudioBackend> logger)
    {
        _logger = logger;

        if (OperatingSystem.IsWindows())
        {
            _wavePlayer = new WaveOutEvent();
        }
        else
        {
            throw new PlatformNotSupportedException("NAudio backend is only supported on Windows");
        }

        _wavePlayer.PlaybackStopped += OnPlaybackStopped;

        _positionTimer = new System.Timers.Timer(100);
        _positionTimer.Elapsed += (s, e) =>
        {
            if (IsPlaying)
                PositionChanged?.Invoke(this, CurrentTime);
        };
        _positionTimer.Start();
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

        Stop();

        _currentIndex = index;
        _currentTrack = _queue[index];

        try
        {
            _audioFileReader?.Dispose();
            _audioFileReader = new AudioFileReader(_currentTrack.FilePath);

            // Set up sample providers for volume control and metering
            _volumeProvider = new VolumeSampleProvider(_audioFileReader)
            {
                Volume = _muted ? 0 : _volume / 100f
            };

            _meteringProvider = new MeteringSampleProvider(_volumeProvider);
            _meteringProvider.StreamVolume += OnStreamVolume;

            _wavePlayer?.Init(_meteringProvider);

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

    private void OnStreamVolume(object? sender, StreamVolumeEventArgs e)
    {
        // Convert metering data to audio samples for visualization
        if (AudioDataAvailable != null && _meteringProvider != null)
        {
            // Create a simple representation from peak values
            var samples = new float[e.MaxSampleValues.Length];
            for (int i = 0; i < e.MaxSampleValues.Length; i++)
            {
                samples[i] = e.MaxSampleValues[i];
            }

            AudioDataAvailable?.Invoke(this, new AudioDataEventArgs
            {
                Samples = samples,
                SampleRate = _audioFileReader?.WaveFormat.SampleRate ?? 44100,
                Channels = _audioFileReader?.WaveFormat.Channels ?? 2
            });
        }
    }

    public async Task PlayAsync()
    {
        if (_audioFileReader == null && _queue.Count > 0)
        {
            LoadTrack(_currentIndex >= 0 ? _currentIndex : 0);
        }

        if (_audioFileReader != null && _wavePlayer != null)
        {
            await Task.Run(() =>
            {
                _wavePlayer.Play();
                _isPlaying = true;
                _isPaused = false;
                PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
                {
                    State = PlaybackState.Playing
                });
            });
        }
    }

    public void Pause()
    {
        if (_wavePlayer == null) return;

        if (_isPlaying)
        {
            _wavePlayer.Pause();
            _isPlaying = false;
            _isPaused = true;
            PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
            {
                State = PlaybackState.Paused
            });
        }
        else if (_isPaused)
        {
            _wavePlayer.Play();
            _isPlaying = true;
            _isPaused = false;
            PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
            {
                State = PlaybackState.Playing
            });
        }
    }

    public void Stop()
    {
        if (_wavePlayer != null)
        {
            _wavePlayer.Stop();
            _isPlaying = false;
            _isPaused = false;
            PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
            {
                State = PlaybackState.Stopped
            });
        }
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
        if (_audioFileReader != null)
        {
            _audioFileReader.CurrentTime = position;
        }
    }

    public void SeekRelative(TimeSpan offset)
    {
        var newPosition = CurrentTime + offset;
        if (newPosition < TimeSpan.Zero) newPosition = TimeSpan.Zero;
        if (newPosition > Duration) newPosition = Duration;
        Seek(newPosition);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, "Playback error occurred");
            ErrorOccurred?.Invoke(this, e.Exception.Message);
            PlaybackStateChanged?.Invoke(this, new PlaybackStateChangedEventArgs
            {
                State = PlaybackState.Error
            });
        }
        else if (_isPlaying)
        {
            // Track ended naturally
            _logger.LogInformation("Track ended, moving to next");
            _isPlaying = false;
            Next();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _positionTimer?.Stop();
        _positionTimer?.Dispose();
        Stop();
        _audioFileReader?.Dispose();
        _wavePlayer?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
