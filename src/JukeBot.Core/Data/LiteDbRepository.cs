using JukeBot.Core.Models;
using LiteDB;
using Microsoft.Extensions.Logging;

namespace JukeBot.Core.Data;

public class LiteDbRepository : IRepository
{
    private readonly LiteDatabase _db;
    private readonly ILogger<LiteDbRepository> _logger;
    private readonly ILiteCollection<Track> _tracks;
    private readonly ILiteCollection<Album> _albums;
    private readonly ILiteCollection<Artist> _artists;
    private readonly ILiteCollection<Playlist> _playlists;
    private readonly ILiteCollection<AppSettings> _settings;

    public LiteDbRepository(string dbPath, ILogger<LiteDbRepository> logger)
    {
        _logger = logger;
        _db = new LiteDatabase(dbPath);

        _tracks = _db.GetCollection<Track>("tracks");
        _albums = _db.GetCollection<Album>("albums");
        _artists = _db.GetCollection<Artist>("artists");
        _playlists = _db.GetCollection<Playlist>("playlists");
        _settings = _db.GetCollection<AppSettings>("settings");

        // Create indices for faster queries
        _tracks.EnsureIndex(t => t.FilePath, true);
        _tracks.EnsureIndex(t => t.AlbumId);
        _tracks.EnsureIndex(t => t.ArtistId);
        _tracks.EnsureIndex(t => t.Title);
        _albums.EnsureIndex(a => a.Name);
        _albums.EnsureIndex(a => a.ArtistId);
        _artists.EnsureIndex(a => a.Name, true);
    }

    // Tracks
    public void AddTrack(Track track)
    {
        _tracks.Insert(track);
        _logger.LogDebug("Added track: {Path}", track.FilePath);
    }

    public void UpdateTrack(Track track)
    {
        _tracks.Update(track);
        _logger.LogDebug("Updated track: {Path}", track.FilePath);
    }

    public void DeleteTrack(int id)
    {
        _tracks.Delete(id);
        _logger.LogDebug("Deleted track: {Id}", id);
    }

    public Track? GetTrack(int id)
    {
        return _tracks.FindById(id);
    }

    public Track? GetTrackByPath(string path)
    {
        return _tracks.FindOne(t => t.FilePath == path);
    }

    public List<Track> GetAllTracks()
    {
        return _tracks.FindAll().ToList();
    }

    public List<Track> GetTracksByAlbum(int albumId)
    {
        return _tracks.Find(t => t.AlbumId == albumId)
            .OrderBy(t => t.DiscNumber)
            .ThenBy(t => t.TrackNumber)
            .ToList();
    }

    public List<Track> GetTracksByArtist(int artistId)
    {
        return _tracks.Find(t => t.ArtistId == artistId).ToList();
    }

    // Albums
    public void AddAlbum(Album album)
    {
        _albums.Insert(album);
        _logger.LogDebug("Added album: {Name}", album.Name);
    }

    public void UpdateAlbum(Album album)
    {
        _albums.Update(album);
        _logger.LogDebug("Updated album: {Name}", album.Name);
    }

    public Album? GetAlbum(int id)
    {
        return _albums.FindById(id);
    }

    public Album? GetAlbumByName(string name, int? artistId)
    {
        if (artistId.HasValue)
        {
            return _albums.FindOne(a => a.Name == name && a.ArtistId == artistId.Value);
        }
        return _albums.FindOne(a => a.Name == name);
    }

    public List<Album> GetAllAlbums()
    {
        return _albums.FindAll().OrderBy(a => a.Name).ToList();
    }

    public List<Album> GetAlbumsByArtist(int artistId)
    {
        return _albums.Find(a => a.ArtistId == artistId)
            .OrderBy(a => a.Year)
            .ThenBy(a => a.Name)
            .ToList();
    }

    // Artists
    public void AddArtist(Artist artist)
    {
        _artists.Insert(artist);
        _logger.LogDebug("Added artist: {Name}", artist.Name);
    }

    public void UpdateArtist(Artist artist)
    {
        _artists.Update(artist);
        _logger.LogDebug("Updated artist: {Name}", artist.Name);
    }

    public Artist? GetArtist(int id)
    {
        return _artists.FindById(id);
    }

    public Artist? GetArtistByName(string name)
    {
        return _artists.FindOne(a => a.Name == name);
    }

    public List<Artist> GetAllArtists()
    {
        return _artists.FindAll().OrderBy(a => a.Name).ToList();
    }

    // Playlists
    public void AddPlaylist(Playlist playlist)
    {
        playlist.CreatedAt = DateTime.UtcNow;
        playlist.UpdatedAt = DateTime.UtcNow;
        _playlists.Insert(playlist);
        _logger.LogInformation("Added playlist: {Name}", playlist.Name);
    }

    public void UpdatePlaylist(Playlist playlist)
    {
        playlist.UpdatedAt = DateTime.UtcNow;
        _playlists.Update(playlist);
        _logger.LogInformation("Updated playlist: {Name}", playlist.Name);
    }

    public void DeletePlaylist(int id)
    {
        _playlists.Delete(id);
        _logger.LogInformation("Deleted playlist: {Id}", id);
    }

    public Playlist? GetPlaylist(int id)
    {
        return _playlists.FindById(id);
    }

    public List<Playlist> GetAllPlaylists()
    {
        return _playlists.FindAll().OrderBy(p => p.Name).ToList();
    }

    public void AddTrackToPlaylist(int playlistId, int trackId)
    {
        var playlist = GetPlaylist(playlistId);
        if (playlist == null)
        {
            _logger.LogWarning("Playlist not found: {Id}", playlistId);
            return;
        }

        var maxPosition = playlist.Items.Any() ? playlist.Items.Max(i => i.Position) : -1;
        var maxId = playlist.Items.Any() ? playlist.Items.Max(i => i.Id) : 0;
        playlist.Items.Add(new PlaylistItem
        {
            Id = maxId + 1,
            PlaylistId = playlistId,
            TrackId = trackId,
            Position = maxPosition + 1,
            AddedAt = DateTime.UtcNow
        });

        UpdatePlaylist(playlist);
    }

    public void RemoveTrackFromPlaylist(int playlistId, int itemId)
    {
        var playlist = GetPlaylist(playlistId);
        if (playlist == null)
        {
            _logger.LogWarning("Playlist not found: {Id}", playlistId);
            return;
        }

        var item = playlist.Items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            playlist.Items.Remove(item);
            // Reorder remaining items
            for (int i = 0; i < playlist.Items.Count; i++)
            {
                playlist.Items[i].Position = i;
            }
            UpdatePlaylist(playlist);
        }
    }

    public void ReorderPlaylistItems(int playlistId, List<PlaylistItem> items)
    {
        var playlist = GetPlaylist(playlistId);
        if (playlist == null)
        {
            _logger.LogWarning("Playlist not found: {Id}", playlistId);
            return;
        }

        playlist.Items = items;
        for (int i = 0; i < playlist.Items.Count; i++)
        {
            playlist.Items[i].Position = i;
        }
        UpdatePlaylist(playlist);
    }

    // Settings
    public AppSettings GetSettings()
    {
        var settings = _settings.FindById(1);
        if (settings == null)
        {
            settings = new AppSettings { Id = 1 };
            _settings.Insert(settings);
        }
        return settings;
    }

    public void SaveSettings(AppSettings settings)
    {
        settings.Id = 1;
        _settings.Upsert(settings);
        _logger.LogDebug("Saved settings");
    }

    // Utility
    public void ClearLibrary()
    {
        _tracks.DeleteAll();
        _albums.DeleteAll();
        _artists.DeleteAll();
        _logger.LogInformation("Cleared library");
    }

    public int GetTrackCount()
    {
        return _tracks.Count();
    }

    public void Dispose()
    {
        _db?.Dispose();
    }
}
