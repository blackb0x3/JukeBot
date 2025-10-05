using JukeBot.Core.Data;
using JukeBot.Core.Models;
using JukeBot.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace JukeBot.Tests;

public class PlaylistServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly LiteDbRepository _repository;
    private readonly PlaylistService _playlistService;

    public PlaylistServiceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_jukebot_{Guid.NewGuid()}.db");
        var repoLogger = new Mock<ILogger<LiteDbRepository>>();
        var serviceLogger = new Mock<ILogger<PlaylistService>>();
        _repository = new LiteDbRepository(_testDbPath, repoLogger.Object);
        _playlistService = new PlaylistService(_repository, serviceLogger.Object);
    }

    [Fact]
    public void Should_Create_Playlist()
    {
        var playlist = _playlistService.CreatePlaylist("Test Playlist", "Description");

        Assert.NotNull(playlist);
        Assert.Equal("Test Playlist", playlist.Name);
        Assert.Equal("Description", playlist.Description);
        Assert.True(playlist.Id > 0);
    }

    [Fact]
    public void Should_List_All_Playlists()
    {
        _playlistService.CreatePlaylist("Playlist 1");
        _playlistService.CreatePlaylist("Playlist 2");

        var playlists = _playlistService.GetAllPlaylists();

        Assert.Equal(2, playlists.Count);
        Assert.Contains(playlists, p => p.Name == "Playlist 1");
        Assert.Contains(playlists, p => p.Name == "Playlist 2");
    }

    [Fact]
    public void Should_Add_Track_To_Playlist()
    {
        var track = new Track
        {
            FilePath = "/test/track.mp3",
            Title = "Test Track",
            Duration = TimeSpan.FromMinutes(3),
            AddedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            FileHash = "hash123"
        };
        _repository.AddTrack(track);

        var playlist = _playlistService.CreatePlaylist("Test Playlist");
        _playlistService.AddTrack(playlist.Id, track.Id);

        var retrieved = _playlistService.GetPlaylist(playlist.Id);
        Assert.NotNull(retrieved);
        Assert.Single(retrieved.Items);
        Assert.Equal(track.Id, retrieved.Items[0].TrackId);
    }

    [Fact]
    public void Should_Remove_Track_From_Playlist()
    {
        var track = new Track
        {
            FilePath = "/test/track.mp3",
            Title = "Test Track",
            Duration = TimeSpan.FromMinutes(3),
            AddedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            FileHash = "hash123"
        };
        _repository.AddTrack(track);

        var playlist = _playlistService.CreatePlaylist("Test Playlist");
        _playlistService.AddTrack(playlist.Id, track.Id);

        var retrieved = _playlistService.GetPlaylist(playlist.Id);
        var item = retrieved!.Items[0];

        _playlistService.RemoveTrack(playlist.Id, item.Id);

        var afterRemoval = _playlistService.GetPlaylist(playlist.Id);
        Assert.NotNull(afterRemoval);
        Assert.Empty(afterRemoval.Items);
    }

    [Fact]
    public void Should_Reorder_Playlist_Items()
    {
        var track1 = new Track
        {
            FilePath = "/test/track1.mp3",
            Title = "Track 1",
            Duration = TimeSpan.FromMinutes(3),
            AddedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            FileHash = "hash1"
        };
        var track2 = new Track
        {
            FilePath = "/test/track2.mp3",
            Title = "Track 2",
            Duration = TimeSpan.FromMinutes(3),
            AddedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            FileHash = "hash2"
        };

        _repository.AddTrack(track1);
        _repository.AddTrack(track2);

        var playlist = _playlistService.CreatePlaylist("Test Playlist");
        _playlistService.AddTrack(playlist.Id, track1.Id);
        _playlistService.AddTrack(playlist.Id, track2.Id);

        var retrieved = _playlistService.GetPlaylist(playlist.Id);
        var item = retrieved!.Items.First(i => i.TrackId == track2.Id);

        _playlistService.MoveTrackUp(playlist.Id, item.Id);

        var afterMove = _playlistService.GetPlaylist(playlist.Id);
        Assert.Equal(track2.Id, afterMove!.Items[0].TrackId);
        Assert.Equal(track1.Id, afterMove.Items[1].TrackId);
    }

    [Fact]
    public void Should_Delete_Playlist()
    {
        var playlist = _playlistService.CreatePlaylist("Test Playlist");
        _playlistService.DeletePlaylist(playlist.Id);

        var retrieved = _playlistService.GetPlaylist(playlist.Id);
        Assert.Null(retrieved);
    }

    public void Dispose()
    {
        _repository.Dispose();
        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);
    }
}
