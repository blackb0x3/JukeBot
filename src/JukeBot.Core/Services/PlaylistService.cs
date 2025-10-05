using JukeBot.Core.Data;
using JukeBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace JukeBot.Core.Services;

public class PlaylistService
{
    private readonly IRepository _repository;
    private readonly ILogger<PlaylistService> _logger;

    public PlaylistService(IRepository repository, ILogger<PlaylistService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Playlist CreatePlaylist(string name, string? description = null)
    {
        var playlist = new Playlist
        {
            Name = name,
            Description = description
        };

        _repository.AddPlaylist(playlist);
        _logger.LogInformation("Created playlist: {Name}", name);
        return playlist;
    }

    public void RenamePlaylist(int playlistId, string newName)
    {
        var playlist = _repository.GetPlaylist(playlistId);
        if (playlist == null)
        {
            _logger.LogWarning("Playlist not found: {Id}", playlistId);
            return;
        }

        playlist.Name = newName;
        _repository.UpdatePlaylist(playlist);
    }

    public void DeletePlaylist(int playlistId)
    {
        _repository.DeletePlaylist(playlistId);
    }

    public List<Playlist> GetAllPlaylists()
    {
        return _repository.GetAllPlaylists();
    }

    public Playlist? GetPlaylist(int playlistId)
    {
        return _repository.GetPlaylist(playlistId);
    }

    public void AddTrack(int playlistId, int trackId)
    {
        _repository.AddTrackToPlaylist(playlistId, trackId);
    }

    public void AddTracks(int playlistId, List<int> trackIds)
    {
        foreach (var trackId in trackIds)
        {
            _repository.AddTrackToPlaylist(playlistId, trackId);
        }
    }

    public void RemoveTrack(int playlistId, int itemId)
    {
        _repository.RemoveTrackFromPlaylist(playlistId, itemId);
    }

    public void MoveTrackUp(int playlistId, int itemId)
    {
        var playlist = _repository.GetPlaylist(playlistId);
        if (playlist == null) return;

        var item = playlist.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null || item.Position == 0) return;

        var swapItem = playlist.Items.FirstOrDefault(i => i.Position == item.Position - 1);
        if (swapItem != null)
        {
            (item.Position, swapItem.Position) = (swapItem.Position, item.Position);
            playlist.Items = playlist.Items.OrderBy(i => i.Position).ToList();
            _repository.UpdatePlaylist(playlist);
        }
    }

    public void MoveTrackDown(int playlistId, int itemId)
    {
        var playlist = _repository.GetPlaylist(playlistId);
        if (playlist == null) return;

        var item = playlist.Items.FirstOrDefault(i => i.Id == itemId);
        if (item == null || item.Position >= playlist.Items.Count - 1) return;

        var swapItem = playlist.Items.FirstOrDefault(i => i.Position == item.Position + 1);
        if (swapItem != null)
        {
            (item.Position, swapItem.Position) = (swapItem.Position, item.Position);
            playlist.Items = playlist.Items.OrderBy(i => i.Position).ToList();
            _repository.UpdatePlaylist(playlist);
        }
    }

    public void Shuffle(int playlistId)
    {
        var playlist = _repository.GetPlaylist(playlistId);
        if (playlist == null) return;

        var random = new Random();
        var shuffled = playlist.Items.OrderBy(x => random.Next()).ToList();
        _repository.ReorderPlaylistItems(playlistId, shuffled);
    }

    public List<Track> GetPlaylistTracks(int playlistId)
    {
        var playlist = _repository.GetPlaylist(playlistId);
        if (playlist == null) return new List<Track>();

        var tracks = new List<Track>();
        foreach (var item in playlist.Items.OrderBy(i => i.Position))
        {
            var track = _repository.GetTrack(item.TrackId);
            if (track != null)
            {
                tracks.Add(track);
            }
        }
        return tracks;
    }

    public void ImportM3U(string filePath, string playlistName)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("M3U file not found: {Path}", filePath);
            return;
        }

        var playlist = CreatePlaylist(playlistName);
        var lines = File.ReadAllLines(filePath);
        var baseDir = Path.GetDirectoryName(filePath) ?? "";

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var trackPath = line;
            if (!Path.IsPathRooted(trackPath))
            {
                trackPath = Path.Combine(baseDir, trackPath);
            }

            var track = _repository.GetTrackByPath(trackPath);
            if (track != null)
            {
                AddTrack(playlist.Id, track.Id);
            }
            else
            {
                _logger.LogWarning("Track not found in library: {Path}", trackPath);
            }
        }

        _logger.LogInformation("Imported M3U playlist: {Name} with {Count} tracks",
            playlistName, playlist.Items.Count);
    }

    public void ExportM3U(int playlistId, string filePath)
    {
        var playlist = _repository.GetPlaylist(playlistId);
        if (playlist == null)
        {
            _logger.LogWarning("Playlist not found: {Id}", playlistId);
            return;
        }

        var tracks = GetPlaylistTracks(playlistId);
        using var writer = new StreamWriter(filePath);

        writer.WriteLine("#EXTM3U");
        foreach (var track in tracks)
        {
            var duration = (int)track.Duration.TotalSeconds;
            var title = $"{track.Artist} - {track.Title}";
            writer.WriteLine($"#EXTINF:{duration},{title}");
            writer.WriteLine(track.FilePath);
        }

        _logger.LogInformation("Exported playlist {Name} to {Path}", playlist.Name, filePath);
    }
}
