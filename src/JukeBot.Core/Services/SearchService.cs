using JukeBot.Core.Data;
using JukeBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace JukeBot.Core.Services;

public class SearchService
{
    private readonly IRepository _repository;
    private readonly ILogger<SearchService> _logger;

    public SearchService(IRepository repository, ILogger<SearchService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public SearchResults Search(string query, SearchFilter filter = SearchFilter.All)
    {
        var results = new SearchResults();

        if (string.IsNullOrWhiteSpace(query))
            return results;

        var lowerQuery = query.ToLowerInvariant();

        if (filter.HasFlag(SearchFilter.Tracks))
        {
            results.Tracks = _repository.GetAllTracks()
                .Where(t =>
                    (t.Title?.ToLowerInvariant().Contains(lowerQuery) ?? false) ||
                    (t.Artist?.ToLowerInvariant().Contains(lowerQuery) ?? false) ||
                    (t.Album?.ToLowerInvariant().Contains(lowerQuery) ?? false) ||
                    t.FilePath.ToLowerInvariant().Contains(lowerQuery))
                .ToList();
        }

        if (filter.HasFlag(SearchFilter.Artists))
        {
            results.Artists = _repository.GetAllArtists()
                .Where(a => a.Name.ToLowerInvariant().Contains(lowerQuery))
                .ToList();
        }

        if (filter.HasFlag(SearchFilter.Albums))
        {
            results.Albums = _repository.GetAllAlbums()
                .Where(a =>
                    a.Name.ToLowerInvariant().Contains(lowerQuery) ||
                    (a.ArtistName?.ToLowerInvariant().Contains(lowerQuery) ?? false))
                .ToList();
        }

        if (filter.HasFlag(SearchFilter.Playlists))
        {
            results.Playlists = _repository.GetAllPlaylists()
                .Where(p => p.Name.ToLowerInvariant().Contains(lowerQuery))
                .ToList();
        }

        _logger.LogDebug("Search for '{Query}': {Tracks} tracks, {Artists} artists, {Albums} albums, {Playlists} playlists",
            query, results.Tracks.Count, results.Artists.Count, results.Albums.Count, results.Playlists.Count);

        return results;
    }

    public List<Track> SearchTracks(string query)
    {
        return Search(query, SearchFilter.Tracks).Tracks;
    }

    public List<Artist> SearchArtists(string query)
    {
        return Search(query, SearchFilter.Artists).Artists;
    }

    public List<Album> SearchAlbums(string query)
    {
        return Search(query, SearchFilter.Albums).Albums;
    }
}

public class SearchResults
{
    public List<Track> Tracks { get; set; } = new();
    public List<Artist> Artists { get; set; } = new();
    public List<Album> Albums { get; set; } = new();
    public List<Playlist> Playlists { get; set; } = new();

    public int TotalCount => Tracks.Count + Artists.Count + Albums.Count + Playlists.Count;
}

[Flags]
public enum SearchFilter
{
    None = 0,
    Tracks = 1,
    Artists = 2,
    Albums = 4,
    Playlists = 8,
    All = Tracks | Artists | Albums | Playlists
}
