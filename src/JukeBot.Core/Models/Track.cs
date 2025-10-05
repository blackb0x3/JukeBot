namespace JukeBot.Core.Models;

public class Track
{
    public int Id { get; set; }
    public required string FilePath { get; set; }
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public int? TrackNumber { get; set; }
    public int? DiscNumber { get; set; }
    public string? Genre { get; set; }
    public int? Year { get; set; }
    public TimeSpan Duration { get; set; }
    public int? Bitrate { get; set; }
    public int? SampleRate { get; set; }
    public int? Channels { get; set; }
    public string? FileHash { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime LastModified { get; set; }

    public int? AlbumId { get; set; }
    public int? ArtistId { get; set; }
}
