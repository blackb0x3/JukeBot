# Audio Quality Guide

## Current Implementation: LibVLC

JukeBot uses LibVLC as the primary audio backend with the following optimizations:

### LibVLC Configuration
```csharp
new LibVLC(
    "--no-video",                          // Disable video processing
    "--quiet",                             // Minimal logging
    "--audio-resampler=soxr",             // SoX Resampler (high quality)
    "--soxr-resampler-quality=10",        // Maximum quality (0-10 scale)
    "--audio-filter=",                    // No audio filters
    "--audio-visual=",                    // No visualizer processing
    "--gain=1.0",                         // Unity gain (no normalization)
    "--volume-save",                      // Remember volume setting
    "--audio-desync=0"                    // No audio desync
);
```

### Why LibVLC?

**Pros**:
- ✅ Cross-platform (Windows, macOS, Linux)
- ✅ Supports all audio formats natively
- ✅ Mature, battle-tested codec implementation
- ✅ SoX resampler is industry-standard high quality
- ✅ No external dependencies beyond VLC installation

**Cons**:
- ⚠️ Larger dependency (requires VLC installation)
- ⚠️ Some Windows audio quirks with default settings
- ⚠️ Can have higher latency than native audio APIs

## Audio Quality Troubleshooting

### If Audio Sounds Poor

1. **Check System Audio Settings** (Windows):
   ```
   - Right-click Volume icon → Sounds
   - Playback tab → Select your device → Properties
   - Advanced tab → Set to highest quality (24-bit, 192000 Hz or 96000 Hz)
   - Disable "Allow applications to take exclusive control"
   ```

2. **Verify VLC Installation**:
   - Ensure you have the latest VLC installed (3.0.20 or newer)
   - Test the same audio file in VLC directly
   - If VLC also sounds poor, it's a system audio issue

3. **Check File Quality**:
   - Low-bitrate MP3s (< 192kbps) may sound poor regardless of player
   - Use lossless formats (FLAC, ALAC) for best quality
   - Check source file isn't corrupted

4. **Volume Settings**:
   - Avoid setting volume > 100% (causes clipping)
   - Check system volume mixer isn't applying effects
   - Disable Windows "Loudness Equalization"

## Alternative Audio Backends

### Option 1: BASS Audio Library (Recommended for Windows)

BASS provides excellent audio quality with low latency:

```bash
# Add BASS.NET package
dotnet add package BASS.NET
```

**Pros**:
- ⭐ Excellent audio quality
- ⭐ Very low latency
- ⭐ Native Windows integration
- ⭐ Lightweight

**Cons**:
- ❌ Windows-only
- ❌ Commercial license required for distribution
- ❌ Not included in current implementation

### Option 2: PortAudio + libsndfile

Pure cross-platform solution:

```bash
# Add PortAudioSharp
dotnet add package PortAudioSharp
dotnet add package libsndfile
```

**Pros**:
- ✅ True cross-platform
- ✅ Very high quality
- ✅ Open source (MIT license)
- ✅ Low latency

**Cons**:
- ⚠️ Requires manual codec handling per format
- ⚠️ More complex implementation
- ⚠️ Native dependencies

### Option 3: OpenAL Soft

Gaming-grade audio:

**Pros**:
- ✅ Cross-platform
- ✅ 3D audio support
- ✅ Very low latency
- ✅ Open source

**Cons**:
- ⚠️ Overkill for music playback
- ⚠️ Requires codec libraries

## Recommended: Stick with LibVLC + Proper Configuration

For most users, LibVLC with proper configuration provides excellent quality:

1. **Install Latest VLC**: Ensures latest codecs and bug fixes
2. **Use SoXR Resampler**: Already configured (quality=10)
3. **Check System Audio**: Often the bottleneck
4. **Use Lossless Sources**: FLAC, ALAC, WAV for best results

## Implementing Alternative Backend

If you want to try a different backend:

1. **Implement `IAudioBackend` interface**:
   ```csharp
   public class BassAudioBackend : IAudioBackend
   {
       // Implement all interface methods
   }
   ```

2. **Register in DI** (`Program.cs`):
   ```csharp
   services.AddSingleton<IAudioBackend>(sp =>
       new BassAudioBackend(logger));
   ```

3. **Test thoroughly** with various formats

## Performance Comparison

| Backend | Quality | Latency | Cross-Platform | License |
|---------|---------|---------|----------------|---------|
| LibVLC (current) | ⭐⭐⭐⭐ | ~50ms | ✅ | GPL |
| BASS | ⭐⭐⭐⭐⭐ | ~10ms | ❌ (Win) | Commercial |
| PortAudio | ⭐⭐⭐⭐⭐ | ~20ms | ✅ | MIT |
| NAudio | ⭐⭐⭐ | ~30ms | ❌ (Win) | MIT |

## Current Status

**JukeBot uses LibVLC with maximum quality settings**. If you're experiencing poor audio quality:

1. First check your system audio configuration
2. Test the same file in VLC Media Player
3. Try different audio files (preferably FLAC)
4. Check Windows audio enhancements are disabled

If quality is still poor after these checks, please open an issue with:
- Audio file format and bitrate
- Operating system and version
- VLC version
- Description of the quality issue

---

**Note**: The audio quality should be comparable to VLC Media Player itself, as we use the same underlying library with optimized settings.
