# JukeBot Quick Start Guide

## Installation & Setup

### 1. Install Prerequisites

**Windows**:
```powershell
# Install .NET 8 SDK
winget install Microsoft.DotNet.SDK.8

# LibVLC is included via NuGet package
```

**macOS**:
```bash
# Install .NET 8 SDK
brew install dotnet@8

# Install VLC (provides LibVLC)
brew install --cask vlc
```

**Linux (Ubuntu/Debian)**:
```bash
# Install .NET 8 SDK
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update
sudo apt install -y dotnet-sdk-8.0

# Install VLC
sudo apt install -y vlc libvlc-dev
```

### 2. Build JukeBot

```bash
cd JukeBot
dotnet restore
dotnet build
```

### 3. First Run

```bash
dotnet run --project src/JukeBot.Tui
```

On first launch:
1. You'll see the JukeBot ASCII logo
2. Answer "Yes" to add a music directory
3. Enter the path to your music folder (e.g., `C:\Users\YourName\Music` or `/home/user/Music`)
4. Wait for the scan to complete
5. Start playing music!

## Using the TUI

### Views

Press number keys to switch views:
- **1** - Library (all tracks)
- **2** - Artists
- **3** - Albums
- **4** - Playlists
- **5** - Now Playing

### Navigation

- **↑/↓** - Move selection up/down
- **Enter** - Play selected track/album/artist
- **F5** - Refresh current view

### Playback Controls

| Key | Action |
|-----|--------|
| `Space` | Play / Pause |
| `N` | Next track |
| `Ctrl+P` | Previous track |
| `←` | Seek backward 5 seconds |
| `→` | Seek forward 5 seconds |
| `Shift+←` | Seek backward 30 seconds |
| `Shift+→` | Seek forward 30 seconds |

### Volume

- **+** or **=** - Increase volume by 5%
- **-** - Decrease volume by 5%
- **M** - Mute/unmute

### Exit

- **Q** - Quit JukeBot

## Using the CLI

### Scan Additional Directories

```bash
dotnet run --project src/JukeBot.Cli -- scan "/path/to/more/music"
```

### Search Your Library

```bash
dotnet run --project src/JukeBot.Cli -- search "Pink Floyd"
```

### View Library Statistics

```bash
dotnet run --project src/JukeBot.Cli -- stats
```

### Manage Playlists

```bash
# List all playlists
dotnet run --project src/JukeBot.Cli -- playlist list

# Create a new playlist
dotnet run --project src/JukeBot.Cli -- playlist create "Favorites"
```

### Import/Export Playlists

```bash
# Import M3U playlist
dotnet run --project src/JukeBot.Cli -- import "myplaylist.m3u" "Imported Playlist"

# Export playlist
dotnet run --project src/JukeBot.Cli -- export "Favorites" "favorites.m3u"
```

### Configure Settings

```bash
# View all settings
dotnet run --project src/JukeBot.Cli -- config

# Set volume
dotnet run --project src/JukeBot.Cli -- config set volume 75

# Add library path
dotnet run --project src/JukeBot.Cli -- config set library-path "/path/to/music"
```

## Tips & Tricks

### Large Libraries

For libraries with 10,000+ tracks:
- Initial scan may take 5-15 minutes
- Subsequent scans are faster (only processes changed files)
- Use F5 to refresh views if data seems stale

### Format Support

Supported formats:
- ✅ MP3
- ✅ M4A/MP4 (AAC)
- ✅ FLAC
- ✅ OGG Vorbis
- ✅ Opus
- ✅ WAV

### Visualizer Not Working?

The visualizer requires:
- LibVLC backend (not NAudio)
- Audio must be actively playing
- If you see "No audio playing", the PCM callback may not be configured

Current version has simplified audio callbacks, so visualizer shows basic level metering.

### Windows Performance

If LibVLC fails on Windows, JukeBot automatically falls back to NAudio.

To force LibVLC:
1. Install VLC Media Player
2. Or ensure `VideoLAN.LibVLC.Windows` NuGet package is restored

### Keyboard Layout

If shortcuts don't work:
- Check your terminal supports key input (Windows Terminal, iTerm2, etc. work well)
- Some terminals may not support Shift+Arrow keys
- Basic playback (Space, N, Q) should work everywhere

## Troubleshooting

### "LibVLC not found"

**Solution**: Install VLC or ensure the NuGet package is restored:
```bash
dotnet restore
dotnet build
```

### Database Corruption

**Solution**: Delete the database and rescan:
```bash
# Windows
del %LocalAppData%\JukeBot\jukebot.db

# macOS/Linux
rm ~/.local/share/JukeBot/jukebot.db
```

Then restart JukeBot and rescan your library.

### No Audio Output

**Check**:
1. Is your volume muted? (Press M to unmute)
2. Is system audio working?
3. Are you using headphones/external speakers? Check connections.
4. Try NAudio backend (Windows only) if LibVLC fails

### Slow Scanning

**Causes**:
- Large files (e.g., lossless FLAC albums)
- Network drives (scanning over network is slow)
- Antivirus scanning files

**Solution**:
- Scan local drives only
- Add music folders to antivirus exclusions
- Be patient! First scan is always slowest

## Next Steps

- Explore different views (Artists → Albums → Tracks)
- Create playlists for different moods
- Import existing M3U playlists
- Check out the visualizer while playing music
- Star the project on GitHub if you like it! 🌟

## Getting Help

- 📖 Full documentation: [README.md](README.md)
- 🏗️ Architecture details: [ARCHITECTURE.md](ARCHITECTURE.md)
- 🐛 Report issues: GitHub Issues
- 💬 Questions: GitHub Discussions

---

**Enjoy JukeBot! 🎵**
