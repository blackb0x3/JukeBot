using JukeBot.Core.Audio;
using JukeBot.Core.Data;
using JukeBot.Core.Models;
using JukeBot.Core.Services;
using JukeBot.Tui.UI;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace JukeBot.Tui;

public class JukeBotApp : IDisposable
{
    private readonly IRepository _repository;
    private readonly IAudioBackend _audioBackend;
    private readonly LibraryScanner _scanner;
    private readonly PlaylistService _playlistService;
    private readonly SearchService _searchService;
    private readonly AudioAnalysisService _analysisService;
    private readonly ILogger<JukeBotApp> _logger;
    private readonly AppSettings _settings;
    private bool _running = true;
    private CancellationTokenSource? _cancellationTokenSource;

    // UI State
    private ViewMode _currentView = ViewMode.Library;
    private int _selectedIndex = 0;
    private List<Track> _currentTracks = new();
    private List<Album> _currentAlbums = new();
    private List<Artist> _currentArtists = new();
    private List<Playlist> _currentPlaylists = new();
    private string? _searchQuery;

    public JukeBotApp(
        IRepository repository,
        IAudioBackend audioBackend,
        LibraryScanner scanner,
        PlaylistService playlistService,
        SearchService searchService,
        AudioAnalysisService analysisService,
        ILogger<JukeBotApp> logger)
    {
        _repository = repository;
        _audioBackend = audioBackend;
        _scanner = scanner;
        _playlistService = playlistService;
        _searchService = searchService;
        _analysisService = analysisService;
        _logger = logger;
        _settings = _repository.GetSettings();

        // Apply settings
        _audioBackend.Volume = _settings.Volume;
        _audioBackend.Muted = _settings.Muted;

        // Attach analysis service to audio backend
        _analysisService.AttachToBackend(_audioBackend);

        // Set up event handlers
        _audioBackend.TrackChanged += (s, e) =>
        {
            _logger.LogInformation("Track changed: {Track}", e.Track?.Title);
        };

        _audioBackend.ErrorOccurred += (s, e) =>
        {
            _logger.LogError("Playback error: {Error}", e);
        };
    }

    public async Task RunAsync()
    {
        Console.CursorVisible = false;
        Console.Clear();

        try
        {
            // Check if library is empty
            if (_repository.GetTrackCount() == 0)
            {
                await ShowFirstRunWizardAsync();
            }

            await MainLoopAsync();
        }
        finally
        {
            Console.CursorVisible = true;
        }
    }

    private async Task ShowFirstRunWizardAsync()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("JukeBot").Centered().Color(Color.Cyan1));
        AnsiConsole.MarkupLine("[yellow]Welcome to JukeBot! Your library is empty.[/]");
        AnsiConsole.WriteLine();

        if (AnsiConsole.Confirm("Would you like to add a music directory?"))
        {
            var path = AnsiConsole.Ask<string>("Enter the [green]path[/] to your music directory:");

            if (Directory.Exists(path))
            {
                _settings.LibraryPaths.Add(path);
                _repository.SaveSettings(_settings);

                await ScanLibraryAsync(path);
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Directory not found![/]");
                await Task.Delay(2000);
            }
        }
    }

    private async Task ScanLibraryAsync(string path)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[cyan]Scanning:[/] {path}");

        var progressTask = AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Scanning files[/]");

                _scanner.ProgressChanged += (s, e) =>
                {
                    task.MaxValue = e.TotalFiles;
                    task.Value = e.ProcessedFiles;
                    task.Description = $"[green]{Path.GetFileName(e.CurrentFile)}[/] ({e.AddedCount} added, {e.UpdatedCount} updated)";
                };

                await _scanner.ScanDirectoryAsync(path);
                task.StopTask();
            });

        await progressTask;
        AnsiConsole.MarkupLine("[green]Scan completed![/]");
        await Task.Delay(1500);
    }

    private async Task MainLoopAsync()
    {
        var renderer = new UIRenderer(_audioBackend, _analysisService);
        var lastRender = DateTime.MinValue;
        var renderInterval = TimeSpan.FromMilliseconds(33); // ~30 FPS

        while (_running)
        {
            // Render UI
            if (DateTime.Now - lastRender >= renderInterval)
            {
                renderer.Render(_currentView, _selectedIndex, GetCurrentItems(), _searchQuery);
                lastRender = DateTime.Now;
            }

            // Handle input
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                await HandleInputAsync(key);
            }

            await Task.Delay(10);
        }
    }

    private object GetCurrentItems()
    {
        return _currentView switch
        {
            ViewMode.Library => _currentTracks,
            ViewMode.Albums => _currentAlbums,
            ViewMode.Artists => _currentArtists,
            ViewMode.Playlists => _currentPlaylists,
            ViewMode.NowPlaying => _currentTracks,
            _ => new List<Track>()
        };
    }

    private async Task HandleInputAsync(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Q:
                _running = false;
                break;

            case ConsoleKey.Spacebar:
                if (_audioBackend.IsPlaying)
                    _audioBackend.Pause();
                else
                    await _audioBackend.PlayAsync();
                break;

            case ConsoleKey.N:
                _audioBackend.Next();
                break;

            case ConsoleKey.P when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                _audioBackend.Previous();
                break;

            case ConsoleKey.RightArrow:
                var seekAmount = key.Modifiers.HasFlag(ConsoleModifiers.Shift)
                    ? TimeSpan.FromSeconds(30)
                    : TimeSpan.FromSeconds(5);
                _audioBackend.SeekRelative(seekAmount);
                break;

            case ConsoleKey.LeftArrow:
                var seekBack = key.Modifiers.HasFlag(ConsoleModifiers.Shift)
                    ? TimeSpan.FromSeconds(-30)
                    : TimeSpan.FromSeconds(-5);
                _audioBackend.SeekRelative(seekBack);
                break;

            case ConsoleKey.UpArrow:
                if (_selectedIndex > 0) _selectedIndex--;
                break;

            case ConsoleKey.DownArrow:
                var itemCount = GetCurrentItemCount();
                if (_selectedIndex < itemCount - 1) _selectedIndex++;
                break;

            case ConsoleKey.Enter:
                await HandleSelectionAsync();
                break;

            case ConsoleKey.F5:
                await RefreshCurrentViewAsync();
                break;

            case ConsoleKey.D1:
                await SwitchToViewAsync(ViewMode.Library);
                break;

            case ConsoleKey.D2:
                await SwitchToViewAsync(ViewMode.Artists);
                break;

            case ConsoleKey.D3:
                await SwitchToViewAsync(ViewMode.Albums);
                break;

            case ConsoleKey.D4:
                await SwitchToViewAsync(ViewMode.Playlists);
                break;

            case ConsoleKey.D5:
                await SwitchToViewAsync(ViewMode.NowPlaying);
                break;

            case ConsoleKey.M:
                _audioBackend.Muted = !_audioBackend.Muted;
                break;

            case ConsoleKey.OemPlus:
            case ConsoleKey.Add:
                _audioBackend.Volume = Math.Min(100, _audioBackend.Volume + 5);
                break;

            case ConsoleKey.OemMinus:
            case ConsoleKey.Subtract:
                _audioBackend.Volume = Math.Max(0, _audioBackend.Volume - 5);
                break;
        }
    }

    private int GetCurrentItemCount()
    {
        return _currentView switch
        {
            ViewMode.Library => _currentTracks.Count,
            ViewMode.Albums => _currentAlbums.Count,
            ViewMode.Artists => _currentArtists.Count,
            ViewMode.Playlists => _currentPlaylists.Count,
            ViewMode.NowPlaying => _currentTracks.Count,
            _ => 0
        };
    }

    private async Task HandleSelectionAsync()
    {
        switch (_currentView)
        {
            case ViewMode.Library:
                if (_selectedIndex < _currentTracks.Count)
                {
                    _audioBackend.LoadQueue(_currentTracks, _selectedIndex);
                    await _audioBackend.PlayAsync();
                }
                break;

            case ViewMode.Artists:
                if (_selectedIndex < _currentArtists.Count)
                {
                    var artist = _currentArtists[_selectedIndex];
                    _currentAlbums = _repository.GetAlbumsByArtist(artist.Id);
                    _currentView = ViewMode.Albums;
                    _selectedIndex = 0;
                }
                break;

            case ViewMode.Albums:
                if (_selectedIndex < _currentAlbums.Count)
                {
                    var album = _currentAlbums[_selectedIndex];
                    _currentTracks = _repository.GetTracksByAlbum(album.Id);
                    _audioBackend.LoadQueue(_currentTracks, 0);
                    await _audioBackend.PlayAsync();
                }
                break;

            case ViewMode.Playlists:
                if (_selectedIndex < _currentPlaylists.Count)
                {
                    var playlist = _currentPlaylists[_selectedIndex];
                    _currentTracks = _playlistService.GetPlaylistTracks(playlist.Id);
                    _audioBackend.LoadQueue(_currentTracks, 0);
                    await _audioBackend.PlayAsync();
                }
                break;
        }
    }

    private async Task SwitchToViewAsync(ViewMode view)
    {
        _currentView = view;
        _selectedIndex = 0;
        await RefreshCurrentViewAsync();
    }

    private async Task RefreshCurrentViewAsync()
    {
        await Task.Run(() =>
        {
            switch (_currentView)
            {
                case ViewMode.Library:
                    _currentTracks = _repository.GetAllTracks();
                    break;

                case ViewMode.Artists:
                    _currentArtists = _repository.GetAllArtists();
                    break;

                case ViewMode.Albums:
                    _currentAlbums = _repository.GetAllAlbums();
                    break;

                case ViewMode.Playlists:
                    _currentPlaylists = _playlistService.GetAllPlaylists();
                    break;
            }
        });
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();

        _settings.Volume = _audioBackend.Volume;
        _settings.Muted = _audioBackend.Muted;
        _repository.SaveSettings(_settings);

        _analysisService?.Dispose();
        _audioBackend?.Dispose();
        _repository?.Dispose();
    }
}

public enum ViewMode
{
    Library,
    Artists,
    Albums,
    Playlists,
    NowPlaying
}
