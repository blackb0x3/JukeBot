# JukeBot Architecture

## Overview

JukeBot is a terminal-based music player built with .NET 8, featuring a layered architecture for maintainability and testability.

## Layers

### 1. Core (`JukeBot.Core`)

Domain logic, services, and cross-platform abstractions.

#### **Models** (`Models/`)
- `Track`: Audio file metadata (title, artist, album, duration, bitrate, etc.)
- `Album`: Album information with track counts and duration
- `Artist`: Artist metadata with counts
- `Playlist` / `PlaylistItem`: Playlist structure with ordering
- `AppSettings`: Application configuration (volume, paths, backend, etc.)

#### **Audio Backends** (`Audio/`)

**`IAudioBackend`**: Core abstraction for audio playback
- Events: `TrackChanged`, `PlaybackStateChanged`, `PositionChanged`, `AudioDataAvailable`, `ErrorOccurred`
- Methods: `LoadQueue`, `PlayAsync`, `Pause`, `Stop`, `Next`, `Previous`, `Seek`, `SeekRelative`
- Properties: `IsPlaying`, `IsPaused`, `CurrentTime`, `Duration`, `Volume`, `Muted`, `CurrentTrack`

**`LibVlcAudioBackend`**: Primary backend (cross-platform)
- Uses LibVLCSharp for playback
- Supports PCM audio callbacks for analysis (simplified in current version)
- Handles all major audio formats via VLC codecs

**`NaudioBackend`**: Windows fallback
- Uses NAudio for playback
- Limited to Windows
- Provides basic metering via `MeteringSampleProvider`

#### **Data Layer** (`Data/`)

**`IRepository`**: Database abstraction (CRUD for all entities)

**`LiteDbRepository`**: LiteDB implementation
- Single-file embedded NoSQL database
- LINQ support for queries
- Automatic indices on paths and names
- Handles relationships (Track → Album → Artist)

#### **Services** (`Services/`)

**`LibraryScanner`**:
- Recursive directory scanning
- Metadata extraction via TagLib-Sharp
- SHA256 file hashing for change detection
- Progress reporting via events
- Idempotent (skip unchanged files)

**`PlaylistService`**:
- CRUD operations for playlists
- Add/remove/reorder tracks
- Shuffle functionality
- M3U import/export

**`SearchService`**:
- Case-insensitive search across tracks, artists, albums, playlists
- Configurable search filters
- Returns `SearchResults` DTO

**`AudioAnalysisService`**:
- FFT-based spectrum analysis using MathNet.Numerics
- Asynchronous processing via `System.Threading.Channels`
- Hann windowing to reduce spectral leakage
- Logarithmic frequency binning (20Hz–20kHz)
- Normalization and smoothing for visualization

### 2. TUI (`JukeBot.Tui`)

Terminal user interface built with Spectre.Console.

#### **`JukeBotApp`**
Main application orchestrator:
- Manages view state (Library, Artists, Albums, Playlists, Now Playing)
- Handles keyboard input
- Coordinates audio backend, services, and UI rendering
- First-run wizard for library setup

#### **`UIRenderer`** (`UI/`)
Renders TUI layout:
- **Header**: ASCII art logo
- **Navigation**: View selection menu
- **Main Content**: Tables for tracks/albums/artists/playlists
- **Visualizer**: Real-time spectrum bars
- **Status Bar**: Playback controls, volume, time

**Features**:
- ~30 FPS rendering
- Responsive keyboard controls
- Paged lists (prevent overflow)
- Color-coded spectrum (green/yellow/red by amplitude)

### 3. CLI (`JukeBot.Cli`)

Headless command-line interface for automation.

**Commands**:
- `scan <path>`: Scan directory and add to library
- `search <query>`: Search library
- `playlist list/create`: Manage playlists
- `import/export`: M3U import/export
- `stats`: Library statistics
- `config set/get`: Configuration management

### 4. Tests (`JukeBot.Tests`)

xUnit test suite with Moq for mocking.

**Coverage**:
- `LibraryScannerTests`: Scanning, progress, metadata extraction
- `PlaylistServiceTests`: CRUD, reordering, M3U
- `SearchServiceTests`: Search across entities, case-insensitivity

## Data Flow

### Playback Flow
```
User Input (TUI) → JukeBotApp
                 → IAudioBackend.LoadQueue()
                 → LibVlcAudioBackend plays track
                 → Events: TrackChanged, PlaybackStateChanged
                 → UI updates via UIRenderer
```

### Library Scanning Flow
```
User triggers scan → LibraryScanner.ScanDirectoryAsync()
                   → For each file:
                       - Compute hash
                       - Extract metadata (TagLib)
                       - Get/create Artist/Album
                       - Add/update Track
                   → Progress events → UI progress bar
```

### Visualizer Flow
```
LibVlcAudioBackend → AudioDataAvailable event (PCM samples)
                   → AudioAnalysisService.OnAudioDataAvailable()
                   → Channel → ProcessAudioDataAsync()
                   → ComputeSpectrum (FFT + binning)
                   → SpectrumUpdated event
                   → UIRenderer renders bars
```

## Dependency Injection

**Registration** (`Program.cs`):
```csharp
services.AddSingleton<IRepository>(LiteDbRepository);
services.AddSingleton<IAudioBackend>(LibVlcAudioBackend or NaudioBackend);
services.AddSingleton<LibraryScanner>();
services.AddSingleton<PlaylistService>();
services.AddSingleton<SearchService>();
services.AddSingleton<AudioAnalysisService>();
services.AddSingleton<JukeBotApp>();
```

**Lifetime**:
- All services are singletons (app is single-user, single-session)
- Repository holds DB connection for app lifetime
- Audio backend initialized once, reused throughout

## Configuration

**Storage**: `%LocalAppData%/JukeBot/jukebot.db` (Windows) or `~/.local/share/JukeBot/jukebot.db` (Unix)

**Settings** (persisted in LiteDB):
- Library paths (list)
- Volume, muted state
- Repeat mode (None/One/All)
- Shuffle enabled
- Audio backend preference
- Visualizer settings (bins, enabled)
- Last playing state (future)

## Threading Model

- **Main Thread**: UI rendering loop (~30 FPS)
- **Audio Thread**: Managed by LibVLC/NAudio
- **Analysis Thread**: `AudioAnalysisService` background task via `System.Threading.Channels`
- **Scan Thread**: `LibraryScanner.ScanDirectoryAsync()` uses TPL (`Task.Run`)

All UI updates are synchronized via Spectre.Console's thread-safe rendering.

## Error Handling

- **Audio Backend Failures**: Graceful fallback (LibVLC → NAudio on Windows)
- **Corrupt Files**: Skip and log, don't crash scanner
- **Missing Metadata**: Use filename as title, "Unknown" as artist
- **Database Errors**: Logged via `ILogger`, exposed to user via TUI messages

## Extensibility Points

1. **New Audio Backend**: Implement `IAudioBackend`, register in DI
2. **Custom Metadata**: Extend `Track` model, modify `LibraryScanner.ExtractMetadata()`
3. **Additional Views**: Add to `ViewMode` enum, implement rendering in `UIRenderer`
4. **New CLI Commands**: Add to `Program.cs` switch statement
5. **Alternative UI**: Replace `JukeBot.Tui` with web UI, keeping `JukeBot.Core` unchanged

## Performance Considerations

- **LiteDB Indices**: Tracks indexed by path (unique), artist/album IDs
- **Scanning**: SHA256 hashing is I/O-bound; async prevents UI blocking
- **FFT**: 2048-sample window is efficient; larger windows increase latency
- **UI Rendering**: Paged lists (20 items) prevent performance degradation with large libraries
- **Channel Buffering**: Audio analysis channel drops oldest data if full (prevents memory bloat)

## Security

- **No Network Access**: Fully offline application
- **File Access**: Only reads from user-specified directories
- **No Credentials**: No authentication or secrets stored
- **Database**: Local-only, no remote connections

## Known Limitations

1. **LibVLC Callbacks**: Simplified implementation (no PCM capture yet)
2. **Visualizer**: Falls back to basic metering if PCM unavailable
3. **NAudio**: Windows-only; not all formats supported
4. **Single Library**: No multiple library profiles (planned)
5. **No Gapless**: Playback has small gaps between tracks
