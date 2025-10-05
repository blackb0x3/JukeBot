namespace JukeBot.Core.Models;

public class Album
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int? Year { get; set; }
    public int? ArtistId { get; set; }
    public string? ArtistName { get; set; }
    public int TrackCount { get; set; }
    public TimeSpan TotalDuration { get; set; }
}
