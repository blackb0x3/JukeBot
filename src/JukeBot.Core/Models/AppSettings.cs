namespace JukeBot.Core.Models;

public class AppSettings
{
    public int Id { get; set; } = 1;
    public List<string> LibraryPaths { get; set; } = new();
    public int Volume { get; set; } = 50;
    public bool Muted { get; set; }
    public RepeatMode RepeatMode { get; set; } = RepeatMode.None;
    public bool Shuffle { get; set; }
    public string AudioBackend { get; set; } = "LibVLC";
    public int SpectrumBins { get; set; } = 24;
    public bool EnableVisualizer { get; set; } = true;
    public string? LastPlayingTrackPath { get; set; }
    public List<int>? LastQueueIds { get; set; }
    public int? LastQueueIndex { get; set; }
}

public enum RepeatMode
{
    None,
    One,
    All
}
