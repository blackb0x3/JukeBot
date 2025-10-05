using JukeBot.Core.Data;
using JukeBot.Core.Models;
using JukeBot.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace JukeBot.Tests;

public class LibraryScannerTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly LiteDbRepository _repository;
    private readonly LibraryScanner _scanner;
    private readonly Mock<ILogger<LiteDbRepository>> _repoLogger;
    private readonly Mock<ILogger<LibraryScanner>> _scannerLogger;

    public LibraryScannerTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_jukebot_{Guid.NewGuid()}.db");
        _repoLogger = new Mock<ILogger<LiteDbRepository>>();
        _scannerLogger = new Mock<ILogger<LibraryScanner>>();
        _repository = new LiteDbRepository(_testDbPath, _repoLogger.Object);
        _scanner = new LibraryScanner(_repository, _scannerLogger.Object);
    }

    [Fact]
    public void Scanner_Should_Initialize()
    {
        Assert.NotNull(_scanner);
    }

    [Fact]
    public async Task Scanner_Should_Report_Progress()
    {
        var progressReported = false;

        _scanner.ProgressChanged += (s, e) =>
        {
            Assert.True(e.TotalFiles >= 0);
            Assert.True(e.ProcessedFiles >= 0);
            Assert.NotNull(e.CurrentFile);
            progressReported = true;
        };

        var testDir = Path.Combine(Path.GetTempPath(), $"test_music_{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);

        try
        {
            // Create a dummy file
            var testFile = Path.Combine(testDir, "test.mp3");
            File.WriteAllText(testFile, "dummy content");

            await _scanner.ScanDirectoryAsync(testDir);

            Assert.True(progressReported);
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public void Repository_Should_Store_And_Retrieve_Track()
    {
        var track = new Track
        {
            FilePath = "/test/path.mp3",
            Title = "Test Track",
            Artist = "Test Artist",
            Album = "Test Album",
            Duration = TimeSpan.FromMinutes(3),
            FileHash = "abc123",
            AddedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        _repository.AddTrack(track);

        var retrieved = _repository.GetTrackByPath("/test/path.mp3");
        Assert.NotNull(retrieved);
        Assert.Equal("Test Track", retrieved.Title);
        Assert.Equal("Test Artist", retrieved.Artist);
    }

    [Fact]
    public void Repository_Should_Handle_Artists()
    {
        var artist = new Artist { Name = "Test Artist" };
        _repository.AddArtist(artist);

        var retrieved = _repository.GetArtistByName("Test Artist");
        Assert.NotNull(retrieved);
        Assert.Equal("Test Artist", retrieved.Name);

        var allArtists = _repository.GetAllArtists();
        Assert.Contains(allArtists, a => a.Name == "Test Artist");
    }

    [Fact]
    public void Repository_Should_Handle_Albums()
    {
        var artist = new Artist { Name = "Test Artist" };
        _repository.AddArtist(artist);

        var album = new Album
        {
            Name = "Test Album",
            ArtistId = artist.Id,
            ArtistName = artist.Name,
            Year = 2024
        };
        _repository.AddAlbum(album);

        var retrieved = _repository.GetAlbumByName("Test Album", artist.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Test Album", retrieved.Name);
        Assert.Equal(2024, retrieved.Year);
    }

    public void Dispose()
    {
        _repository.Dispose();
        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);
    }
}
