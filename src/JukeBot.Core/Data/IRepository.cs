using JukeBot.Core.Models;

namespace JukeBot.Core.Data;

public interface IRepository : IDisposable
{
    // Tracks
    void AddTrack(Track track);
    void UpdateTrack(Track track);
    void DeleteTrack(int id);
    Track? GetTrack(int id);
    Track? GetTrackByPath(string path);
    List<Track> GetAllTracks();
    List<Track> GetTracksByAlbum(int albumId);
    List<Track> GetTracksByArtist(int artistId);

    // Albums
    void AddAlbum(Album album);
    void UpdateAlbum(Album album);
    Album? GetAlbum(int id);
    Album? GetAlbumByName(string name, int? artistId);
    List<Album> GetAllAlbums();
    List<Album> GetAlbumsByArtist(int artistId);

    // Artists
    void AddArtist(Artist artist);
    void UpdateArtist(Artist artist);
    Artist? GetArtist(int id);
    Artist? GetArtistByName(string name);
    List<Artist> GetAllArtists();

    // Playlists
    void AddPlaylist(Playlist playlist);
    void UpdatePlaylist(Playlist playlist);
    void DeletePlaylist(int id);
    Playlist? GetPlaylist(int id);
    List<Playlist> GetAllPlaylists();
    void AddTrackToPlaylist(int playlistId, int trackId);
    void RemoveTrackFromPlaylist(int playlistId, int itemId);
    void ReorderPlaylistItems(int playlistId, List<PlaylistItem> items);

    // Settings
    AppSettings GetSettings();
    void SaveSettings(AppSettings settings);

    // Utility
    void ClearLibrary();
    int GetTrackCount();
}
