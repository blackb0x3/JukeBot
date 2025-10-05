using System.Security.Cryptography;
using JukeBot.Core.Data;
using JukeBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace JukeBot.Core.Services;

public class LibraryScanner
{
    private readonly IRepository _repository;
    private readonly ILogger<LibraryScanner> _logger;
    private static readonly string[] SupportedExtensions = { ".mp3", ".m4a", ".mp4", ".opus", ".flac", ".wav", ".ogg" };

    public event EventHandler<ScanProgressEventArgs>? ProgressChanged;
    public event EventHandler? ScanCompleted;

    public LibraryScanner(IRepository repository, ILogger<LibraryScanner> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task ScanDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(path))
        {
            _logger.LogWarning("Directory does not exist: {Path}", path);
            return;
        }

        _logger.LogInformation("Starting scan of: {Path}", path);

        var files = await Task.Run(() =>
            Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList(),
            cancellationToken);

        _logger.LogInformation("Found {Count} audio files", files.Count);

        int processed = 0;
        int added = 0;
        int updated = 0;
        int skipped = 0;

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Scan cancelled");
                break;
            }

            try
            {
                var result = await ProcessFileAsync(file);
                switch (result)
                {
                    case ScanResult.Added:
                        added++;
                        break;
                    case ScanResult.Updated:
                        updated++;
                        break;
                    case ScanResult.Skipped:
                        skipped++;
                        break;
                }

                processed++;
                ProgressChanged?.Invoke(this, new ScanProgressEventArgs
                {
                    TotalFiles = files.Count,
                    ProcessedFiles = processed,
                    CurrentFile = file,
                    AddedCount = added,
                    UpdatedCount = updated,
                    SkippedCount = skipped
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file: {File}", file);
            }
        }

        _logger.LogInformation("Scan completed. Added: {Added}, Updated: {Updated}, Skipped: {Skipped}",
            added, updated, skipped);

        ScanCompleted?.Invoke(this, EventArgs.Empty);
    }

    private async Task<ScanResult> ProcessFileAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            return ScanResult.Skipped;

        // Check if file already exists in database
        var existingTrack = _repository.GetTrackByPath(filePath);
        var fileHash = await ComputeFileHashAsync(filePath);

        // Skip if file hasn't changed
        if (existingTrack != null && existingTrack.FileHash == fileHash)
        {
            return ScanResult.Skipped;
        }

        // Extract metadata
        var track = await Task.Run(() => ExtractMetadata(filePath, fileHash, fileInfo.LastWriteTime));

        if (track == null)
        {
            _logger.LogWarning("Failed to extract metadata from: {File}", filePath);
            return ScanResult.Skipped;
        }

        // Get or create artist
        if (!string.IsNullOrEmpty(track.Artist))
        {
            var artist = _repository.GetArtistByName(track.Artist);
            if (artist == null)
            {
                artist = new Artist { Name = track.Artist };
                _repository.AddArtist(artist);
            }
            track.ArtistId = artist.Id;

            // Update artist stats
            artist.TrackCount = _repository.GetTracksByArtist(artist.Id).Count + 1;
            _repository.UpdateArtist(artist);
        }

        // Get or create album
        if (!string.IsNullOrEmpty(track.Album))
        {
            var album = _repository.GetAlbumByName(track.Album, track.ArtistId);
            if (album == null)
            {
                album = new Album
                {
                    Name = track.Album,
                    ArtistId = track.ArtistId,
                    ArtistName = track.Artist,
                    Year = track.Year
                };
                _repository.AddAlbum(album);
            }
            track.AlbumId = album.Id;

            // Update album stats
            var albumTracks = _repository.GetTracksByAlbum(album.Id);
            album.TrackCount = albumTracks.Count + 1;
            album.TotalDuration = albumTracks.Aggregate(TimeSpan.Zero, (sum, t) => sum + t.Duration) + track.Duration;
            _repository.UpdateAlbum(album);
        }

        if (existingTrack != null)
        {
            track.Id = existingTrack.Id;
            track.AddedAt = existingTrack.AddedAt;
            _repository.UpdateTrack(track);
            return ScanResult.Updated;
        }
        else
        {
            _repository.AddTrack(track);
            return ScanResult.Added;
        }
    }

    private Track? ExtractMetadata(string filePath, string fileHash, DateTime lastModified)
    {
        try
        {
            using var tagFile = TagLib.File.Create(filePath);

            var track = new Track
            {
                FilePath = filePath,
                FileHash = fileHash,
                Title = tagFile.Tag.Title ?? Path.GetFileNameWithoutExtension(filePath),
                Artist = tagFile.Tag.FirstPerformer ?? tagFile.Tag.FirstAlbumArtist,
                Album = tagFile.Tag.Album,
                TrackNumber = (int?)tagFile.Tag.Track,
                DiscNumber = (int?)tagFile.Tag.Disc,
                Genre = tagFile.Tag.FirstGenre,
                Year = (int?)tagFile.Tag.Year,
                Duration = tagFile.Properties.Duration,
                Bitrate = tagFile.Properties.AudioBitrate,
                SampleRate = tagFile.Properties.AudioSampleRate,
                Channels = tagFile.Properties.AudioChannels,
                AddedAt = DateTime.UtcNow,
                LastModified = lastModified
            };

            return track;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract metadata from: {File}", filePath);
            return null;
        }
    }

    private async Task<string> ComputeFileHashAsync(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}

public class ScanProgressEventArgs : EventArgs
{
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public required string CurrentFile { get; set; }
    public int AddedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }

    public double ProgressPercentage => TotalFiles > 0 ? (ProcessedFiles * 100.0 / TotalFiles) : 0;
}

public enum ScanResult
{
    Added,
    Updated,
    Skipped
}
