# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

JukeBot is a cross-platform terminal music player (TUI) built with .NET 8. It uses LibVLC for audio playback, Spectre.Console for the terminal UI, TagLib-Sharp for metadata extraction, and LiteDB for persistence.

## Build and Test Commands

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/JukeBot.Core/JukeBot.Core.csproj

# Run TUI application
dotnet run --project src/JukeBot.Tui

# Run CLI application
dotnet run --project src/JukeBot.Cli -- <command> [args]

# Run all tests
dotnet test

# Run tests without rebuild
dotnet test --no-build

# Run specific test
dotnet test --filter "FullyQualifiedName~JukeBot.Tests.PlaylistServiceTests.Should_Add_Track_To_Playlist"
```

## Architecture

### Layered Design

**JukeBot.Core** (domain logic, no UI dependencies):
- `IAudioBackend` abstraction with `LibVlcAudioBackend` (cross-platform) and `NaudioBackend` (Windows fallback)
- `IRepository` abstraction with `LiteDbRepository` implementation
- Services are stateless and injected via DI

**JukeBot.Tui** (terminal UI):
- `JukeBotApp` orchestrates the main loop, input handling, and view state
- `UIRenderer` handles all Spectre.Console rendering
- No direct business logic - delegates to Core services

**JukeBot.Cli** (headless commands):
- Reuses Core services for scanning, search, playlist management
- No UI dependencies

### Key Abstractions

**`IAudioBackend`**: All audio implementations must provide:
- Event-based state changes (`TrackChanged`, `PlaybackStateChanged`, `PositionChanged`)
- Queue management with next/previous navigation
- Non-blocking playback (`async Task PlayAsync()`)

**`IRepository`**: All data access goes through this interface:
- CRUD for Track, Album, Artist, Playlist
- Implements `IDisposable` (DB connection lifecycle)
- Returns domain models, not DTOs

### Critical Design Patterns

**Audio Backend Selection**: Program.cs tries LibVLC first, falls back to NAudio on Windows if LibVLC fails. The fallback logic includes exception handling with `Markup.Escape()` for safe error display in Spectre.Console.

**Visualizer Simulation**: `AudioAnalysisService` runs a simulation task (`SimulateSpectrumAsync`) that generates music-like spectrum data since LibVLC PCM callbacks are not fully implemented. It checks `_backend?.IsPlaying` to determine when to generate data.

**Markup Safety**: When displaying user-generated or exception text in Spectre.Console, always use `Markup.Escape()` or `Text(string, Style)` instead of `Markup` to prevent square brackets from being parsed as markup tags.

## Common Pitfalls

**Spectre.Console Markup Exceptions**: Any string with square brackets `[...]` in a `Markup` or `MarkupLine` call will cause parsing exceptions. Use:
```csharp
// Safe
new Text(progressBar, new Style(Color.Cyan1))
AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");

// Unsafe
new Markup($"[cyan]{progressBar}[/]")  // Fails if progressBar contains []
```

**LibVLC Options**: VLC 3.0+ removed several options. Only use:
- `--audio-resampler=soxr` and `--soxr-resampler-quality=10` (proven to work)
- Avoid deprecated options like `--audio-time-stretch=0` or `--sout-transcode-hurry-up`

**Repository Lifecycle**: `LiteDbRepository` implements `IDisposable`. Registered as singleton in DI, disposed automatically on app shutdown. Never create multiple instances.

**Playlist Item IDs**: `PlaylistItem.Id` must be manually assigned in `AddTrackToPlaylist` since LiteDB doesn't auto-generate IDs for nested collections:
```csharp
var maxId = playlist.Items.Any() ? playlist.Items.Max(i => i.Id) : 0;
playlist.Items.Add(new PlaylistItem { Id = maxId + 1, ... });
```

## Testing

Tests use Moq for mocking `ILogger` dependencies. Each test creates a temporary LiteDB file and disposes it in `Dispose()`:
```csharp
_testDbPath = Path.Combine(Path.GetTempPath(), $"test_jukebot_{Guid.NewGuid()}.db");
```

Repository tests verify CRUD operations and relationship handling (Track ↔ Album ↔ Artist).

Service tests verify business logic without UI dependencies.

## LibVLC Configuration

Current high-quality settings (src/JukeBot.Core/Audio/LibVlcAudioBackend.cs):
```csharp
new LibVLC(
    "--no-video",
    "--quiet",
    "--audio-resampler=soxr",
    "--soxr-resampler-quality=10",  // Max quality
    "--audio-filter=",               // No filters
    "--audio-visual=",               // No viz processing
    "--gain=1.0",
    "--volume-save",
    "--audio-desync=0"
);
```

If users report audio quality issues, first check:
1. Windows audio settings (exclusive mode, enhancements)
2. Test same file in VLC Media Player
3. File quality (bitrate, format)

See `AUDIO_QUALITY.md` for troubleshooting guide.

## Database Schema

LiteDB collections:
- `tracks`: All audio files with metadata
- `albums`: Album aggregates with TrackCount and TotalDuration
- `artists`: Artist aggregates with AlbumCount and TrackCount
- `playlists`: User playlists with nested `PlaylistItem[]`
- `settings`: Single document (Id=1) for app configuration

**Important**: Track/Album/Artist relationships use integer IDs. `LibraryScanner` creates/updates these relationships during scan.

**Location**: `%LocalAppData%\JukeBot\jukebot.db` (Windows) or `~/.local/share/JukeBot/jukebot.db` (Unix)

## Adding New Audio Formats

TagLib-Sharp handles format detection automatically. To add support:
1. Add extension to `LibraryScanner.SupportedExtensions`
2. Ensure LibVLC supports the codec (most are built-in)
3. No changes needed to metadata extraction

## Extending the UI

New views require:
1. Add to `ViewMode` enum (JukeBotApp.cs)
2. Add keyboard shortcut in `HandleInputAsync`
3. Add render case in `UIRenderer.RenderMainContent`
4. Add refresh case in `RefreshCurrentViewAsync`

Keep rendering at ~30 FPS - check `DateTime.Now - lastRender >= renderInterval` in main loop.

## Known Issues

- Visualizer uses simulation mode (PCM callbacks not implemented)
- No gapless playback (small gaps between tracks)
- NAudio fallback is Windows-only
- Single library profile (no multiple libraries)
