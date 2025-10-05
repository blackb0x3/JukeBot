# JukeBot – Terminal Music Player

![JukeBot](https://img.shields.io/badge/platform-cross--platform-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

A powerful, feature-rich **terminal music player** built with .NET 8. JukeBot brings a modern TUI experience with real-time audio visualization, library management, and playlist support—all within your terminal.

## ✨ Features

- 🎵 **Multi-format Support**: MP3, M4A/MP4 (AAC), OPUS, FLAC, WAV, OGG
- 🎨 **Live Audio Visualizer**: Real-time FFT-based spectrum analyzer (24 frequency bins)
- 📚 **Library Management**: Automatic scanning, metadata extraction, and organization
- 🎼 **Smart Organization**: Browse by artists, albums, directories, or all tracks
- 📝 **Playlist Support**: Create, edit, and manage custom playlists
- 💾 **M3U Import/Export**: Full compatibility with M3U/M3U8 playlist format
- 🔍 **Fast Search**: Search across tracks, artists, albums, and playlists
- ⚡ **Responsive Controls**: Non-blocking playback with intuitive keyboard shortcuts
- 🎚️ **Playback Control**: Seek, volume control, repeat modes, shuffle
- 💾 **Persistent State**: LiteDB-backed storage for library and settings
- 🖥️ **Cross-platform**: Windows, macOS, Linux (via LibVLC)

## 🎯 Architecture Overview

### Tech Stack

- **TUI Framework**: Spectre.Console for rich terminal UI with tables, progress bars, and live rendering
- **Audio Backend**:
  - **Primary**: LibVLCSharp (cross-platform, extensive codec support)
  - **Fallback**: NAudio (Windows-only)
- **Metadata**: TagLib-Sharp for ID3v1/v2, Vorbis comments, MP4 atoms
- **Database**: LiteDB (embedded NoSQL with LINQ)
- **DSP**: MathNet.Numerics for FFT and spectrum analysis
- **DI**: Microsoft.Extensions.DependencyInjection

### Project Structure

```
JukeBot.sln
├── src/
│   ├── JukeBot.Core/           # Domain models, services, audio backends
│   │   ├── Audio/              # IAudioBackend, LibVLC, NAudio implementations
│   │   ├── Data/               # LiteDB repository
│   │   ├── Models/             # Track, Album, Artist, Playlist DTOs
│   │   └── Services/           # Scanner, Playlist, Search, AudioAnalysis
│   ├── JukeBot.Tui/            # Terminal UI application
│   │   ├── UI/                 # UIRenderer, visualizer
│   │   ├── JukeBotApp.cs       # Main app logic
│   │   └── Program.cs          # Entry point
│   └── JukeBot.Cli/            # Command-line interface
│       └── Program.cs          # CLI commands
└── tests/
    └── JukeBot.Tests/          # xUnit tests
```

## 🚀 Getting Started

### Prerequisites

1. **.NET 8 SDK**
   ```bash
   # Verify installation
   dotnet --version
   ```

2. **LibVLC** (cross-platform audio)

   **Windows**:
   - The project includes `VideoLAN.LibVLC.Windows` NuGet package
   - OR install VLC Media Player: https://www.videolan.org/vlc/

   **macOS**:
   ```bash
   brew install --cask vlc
   ```

   **Linux (Ubuntu/Debian)**:
   ```bash
   sudo apt update
   sudo apt install vlc libvlc-dev
   ```

   **Linux (Fedora)**:
   ```bash
   sudo dnf install vlc vlc-devel
   ```

### Building from Source

```bash
# Clone the repository
git clone https://github.com/yourusername/jukebot.git
cd jukebot

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run TUI application
dotnet run --project src/JukeBot.Tui

# Or run CLI
dotnet run --project src/JukeBot.Cli -- help
```

### Running Tests

```bash
dotnet test
```

## 🎮 Usage

### TUI Application

Launch the interactive terminal UI:

```bash
dotnet run --project src/JukeBot.Tui
```

#### Keyboard Shortcuts

| Key | Action |
|-----|--------|
| **Playback** |
| `Space` | Play / Pause |
| `N` | Next track |
| `Ctrl+P` | Previous track |
| `←` / `→` | Seek ±5 seconds |
| `Shift+←` / `→` | Seek ±30 seconds |
| **Navigation** |
| `1-5` | Switch views (Library, Artists, Albums, Playlists, Now Playing) |
| `↑` / `↓` | Navigate items |
| `Enter` | Select / Play |
| `F5` | Refresh current view |
| **Volume** |
| `+` / `-` | Volume ±5% |
| `M` | Mute / Unmute |
| **General** |
| `Q` | Quit |

#### First Run

On first launch, JukeBot will:
1. Ask if you want to add a music directory
2. Scan the directory and index all supported audio files
3. Extract metadata (title, artist, album, duration, etc.)
4. Launch the TUI with your library ready

### CLI Application

Headless commands for automation and scripting:

```bash
# Scan a directory
dotnet run --project src/JukeBot.Cli -- scan "/path/to/music"

# Search library
dotnet run --project src/JukeBot.Cli -- search "Pink Floyd"

# Playlist management
dotnet run --project src/JukeBot.Cli -- playlist list
dotnet run --project src/JukeBot.Cli -- playlist create "My Playlist"

# Import/Export M3U
dotnet run --project src/JukeBot.Cli -- import "playlist.m3u" "Imported Playlist"
dotnet run --project src/JukeBot.Cli -- export "My Playlist" "output.m3u"

# Library statistics
dotnet run --project src/JukeBot.Cli -- stats

# Configuration
dotnet run --project src/JukeBot.Cli -- config
dotnet run --project src/JukeBot.Cli -- config set volume 75
dotnet run --project src/JukeBot.Cli -- config set library-path "/path/to/music"
```

## 📊 Visualizer

The real-time audio visualizer displays:
- **24 frequency bins** rendered as ASCII block characters
- **~20 FPS rendering** in Spectre.Console
- **Color-coded bars**: Green (low), Yellow (medium), Red (high)
- **Music-like simulation** when audio callbacks unavailable

The visualizer uses a simulation mode that generates spectrum data based on music-like patterns with bass emphasis and temporal variation. Future versions will implement true FFT-based analysis using PCM audio callbacks from LibVLC.

## 🗄️ Data Storage

JukeBot stores all data in a single LiteDB database:

**Location**: `%LocalAppData%\JukeBot\jukebot.db` (Windows) or `~/.local/share/JukeBot/jukebot.db` (macOS/Linux)

**Collections**:
- `tracks` – All indexed audio files with metadata
- `albums` – Album information and track counts
- `artists` – Artist statistics
- `playlists` – User-created playlists
- `settings` – Application configuration

**Indices**:
- File paths (unique)
- Artist/Album relationships
- Track/Album/Artist names for search

## 🔧 Configuration

Settings are stored in the database and can be modified via:
1. TUI (upcoming feature)
2. CLI: `dotnet run --project src/JukeBot.Cli -- config set <key> <value>`
3. Direct database access (advanced)

**Available Settings**:
- `Volume` (0-100)
- `Muted` (boolean)
- `RepeatMode` (None, One, All)
- `Shuffle` (boolean)
- `AudioBackend` (LibVLC, NAudio)
- `LibraryPaths` (list of directories)
- `SpectrumBins` (visualizer frequency bins)
- `EnableVisualizer` (boolean)

## 🐛 Troubleshooting

### "LibVLC not found" error

**Windows**:
- Install VLC Media Player, or
- Ensure `VideoLAN.LibVLC.Windows` NuGet package is restored

**macOS**:
```bash
brew install --cask vlc
```

**Linux**:
```bash
sudo apt install vlc libvlc-dev
```

### Audio not playing on Windows

If LibVLC fails, JukeBot will automatically fall back to NAudio (Windows-only). Check console output for backend selection.

### Corrupt database

Delete the database file and rescan your library:
```bash
# Windows
del %LocalAppData%\JukeBot\jukebot.db

# macOS/Linux
rm ~/.local/share/JukeBot/jukebot.db
```

### Visualizer not working

- Ensure `EnableVisualizer` is `true` in settings
- Check that audio backend supports PCM callbacks (LibVLC does, NAudio provides basic metering)
- Increase `SpectrumBins` for more detailed visualization (16-64 recommended)

## 🧪 Testing

The test suite covers:
- **LibraryScanner**: Metadata extraction and file hashing
- **Repository**: CRUD operations for tracks, albums, artists, playlists
- **PlaylistService**: Create, add, remove, reorder playlist items
- **SearchService**: Case-insensitive search across all entities

Run tests:
```bash
dotnet test
```

## 🛠️ Development

### Adding a New Audio Backend

1. Implement `IAudioBackend` in `JukeBot.Core/Audio/`
2. Register in DI container (`Program.cs`)
3. Handle events: `TrackChanged`, `PlaybackStateChanged`, `AudioDataAvailable`

### Extending Metadata Support

TagLib-Sharp supports most audio formats out of the box. For custom tags, modify `LibraryScanner.ExtractMetadata()`.

### Custom UI Themes

Spectre.Console colors are defined in `UIRenderer.cs`. Modify colors to match your terminal theme.

## 📝 Roadmap

- [ ] File watcher for automatic library updates
- [ ] Gapless playback / crossfade
- [ ] ReplayGain support
- [ ] Cover art display (ASCII/Unicode thumbnails)
- [ ] Lyrics display
- [ ] Last.fm scrobbling
- [ ] Equalizer controls
- [ ] Hotkey customization
- [ ] Multiple library profiles
- [ ] Remote control API

## 🤝 Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Write tests for new functionality
4. Submit a pull request

## 📄 License

MIT License - see LICENSE file for details.

## 🙏 Acknowledgments

- **LibVLCSharp** – Cross-platform audio playback
- **NAudio** – Windows audio fallback
- **TagLib-Sharp** – Metadata extraction
- **Spectre.Console** – Beautiful terminal UI
- **LiteDB** – Embedded database
- **MathNet.Numerics** – FFT implementation

---

**Enjoy your music in the terminal! 🎵**

For issues, feature requests, or questions, please open an issue on GitHub.
