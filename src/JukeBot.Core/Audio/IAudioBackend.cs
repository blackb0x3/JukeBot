using JukeBot.Core.Models;

namespace JukeBot.Core.Audio;

public interface IAudioBackend : IDisposable
{
    event EventHandler<TrackChangedEventArgs>? TrackChanged;
    event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;
    event EventHandler<TimeSpan>? PositionChanged;
    event EventHandler<AudioDataEventArgs>? AudioDataAvailable;
    event EventHandler<string>? ErrorOccurred;

    bool IsPlaying { get; }
    bool IsPaused { get; }
    TimeSpan CurrentTime { get; }
    TimeSpan Duration { get; }
    int Volume { get; set; }
    bool Muted { get; set; }
    Track? CurrentTrack { get; }

    void LoadQueue(List<Track> tracks, int startIndex = 0);
    Task PlayAsync();
    void Pause();
    void Stop();
    void Next();
    void Previous();
    void Seek(TimeSpan position);
    void SeekRelative(TimeSpan offset);
}

public class TrackChangedEventArgs : EventArgs
{
    public Track? Track { get; set; }
    public int QueueIndex { get; set; }
}

public class PlaybackStateChangedEventArgs : EventArgs
{
    public PlaybackState State { get; set; }
}

public class AudioDataEventArgs : EventArgs
{
    public required float[] Samples { get; init; }
    public int SampleRate { get; init; }
    public int Channels { get; init; }
}

public enum PlaybackState
{
    Stopped,
    Playing,
    Paused,
    Buffering,
    Error
}
