namespace JukeBot.Core.Models;

public class Artist
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int AlbumCount { get; set; }
    public int TrackCount { get; set; }
}
