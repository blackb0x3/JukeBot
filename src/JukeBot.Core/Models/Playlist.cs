namespace JukeBot.Core.Models;

public class Playlist
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<PlaylistItem> Items { get; set; } = new();
}

public class PlaylistItem
{
    public int Id { get; set; }
    public int PlaylistId { get; set; }
    public int TrackId { get; set; }
    public int Position { get; set; }
    public DateTime AddedAt { get; set; }
}
