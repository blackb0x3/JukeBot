using JukeBot.Core.Data;
using JukeBot.Core.Models;
using JukeBot.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace JukeBot.Tests;

public class SearchServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly LiteDbRepository _repository;
    private readonly SearchService _searchService;

    public SearchServiceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_jukebot_{Guid.NewGuid()}.db");
        var repoLogger = new Mock<ILogger<LiteDbRepository>>();
        var searchLogger = new Mock<ILogger<SearchService>>();
        _repository = new LiteDbRepository(_testDbPath, repoLogger.Object);
        _searchService = new SearchService(_repository, searchLogger.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var artist = new Artist { Name = "Pink Floyd" };
        _repository.AddArtist(artist);

        var album = new Album
        {
            Name = "The Dark Side of the Moon",
            ArtistId = artist.Id,
            ArtistName = artist.Name,
            Year = 1973
        };
        _repository.AddAlbum(album);

        var track1 = new Track
        {
            FilePath = "/music/time.mp3",
            Title = "Time",
            Artist = "Pink Floyd",
            Album = "The Dark Side of the Moon",
            ArtistId = artist.Id,
            AlbumId = album.Id,
            Duration = TimeSpan.FromMinutes(7),
            AddedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            FileHash = "hash1"
        };

        var track2 = new Track
        {
            FilePath = "/music/money.mp3",
            Title = "Money",
            Artist = "Pink Floyd",
            Album = "The Dark Side of the Moon",
            ArtistId = artist.Id,
            AlbumId = album.Id,
            Duration = TimeSpan.FromMinutes(6),
            AddedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            FileHash = "hash2"
        };

        _repository.AddTrack(track1);
        _repository.AddTrack(track2);
    }

    [Fact]
    public void Should_Search_Tracks_By_Title()
    {
        var results = _searchService.SearchTracks("Time");

        Assert.Single(results);
        Assert.Equal("Time", results[0].Title);
    }

    [Fact]
    public void Should_Search_Tracks_By_Artist()
    {
        var results = _searchService.SearchTracks("Pink Floyd");

        Assert.Equal(2, results.Count);
        Assert.All(results, t => Assert.Equal("Pink Floyd", t.Artist));
    }

    [Fact]
    public void Should_Search_Artists()
    {
        var results = _searchService.SearchArtists("Pink");

        Assert.Single(results);
        Assert.Equal("Pink Floyd", results[0].Name);
    }

    [Fact]
    public void Should_Search_Albums()
    {
        var results = _searchService.SearchAlbums("Dark Side");

        Assert.Single(results);
        Assert.Equal("The Dark Side of the Moon", results[0].Name);
    }

    [Fact]
    public void Should_Return_Empty_Results_For_No_Match()
    {
        var results = _searchService.Search("Nonexistent");

        Assert.Equal(0, results.TotalCount);
        Assert.Empty(results.Tracks);
        Assert.Empty(results.Artists);
        Assert.Empty(results.Albums);
    }

    [Fact]
    public void Should_Search_Case_Insensitive()
    {
        var results = _searchService.SearchTracks("TIME");

        Assert.Single(results);
        Assert.Equal("Time", results[0].Title);
    }

    [Fact]
    public void Should_Search_All_Categories()
    {
        var results = _searchService.Search("Floyd", SearchFilter.All);

        Assert.Equal(2, results.Tracks.Count);
        Assert.Single(results.Artists);
        Assert.True(results.TotalCount >= 3);
    }

    public void Dispose()
    {
        _repository.Dispose();
        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);
    }
}
