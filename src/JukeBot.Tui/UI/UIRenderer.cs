using JukeBot.Core.Audio;
using JukeBot.Core.Models;
using JukeBot.Core.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace JukeBot.Tui.UI;

public class UIRenderer
{
    private readonly IAudioBackend _audioBackend;
    private readonly AudioAnalysisService _analysisService;
    private float[] _lastSpectrum = Array.Empty<float>();

    public UIRenderer(IAudioBackend audioBackend, AudioAnalysisService analysisService)
    {
        _audioBackend = audioBackend;
        _analysisService = analysisService;

        _analysisService.SpectrumUpdated += (s, e) =>
        {
            _lastSpectrum = e.Spectrum;
        };
    }

    public void Render(ViewMode view, int selectedIndex, object items, string? searchQuery)
    {
        Console.SetCursorPosition(0, 0);

        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3),
                new Layout("Body").SplitColumns(
                    new Layout("Navigation").Size(20),
                    new Layout("Main"),
                    new Layout("Visualizer").Size(30)
                ),
                new Layout("StatusBar").Size(3)
            );

        // Header
        layout["Header"].Update(RenderHeader());

        // Navigation
        layout["Navigation"].Update(RenderNavigation(view));

        // Main content
        layout["Main"].Update(RenderMainContent(view, selectedIndex, items, searchQuery));

        // Visualizer
        layout["Visualizer"].Update(RenderVisualizer());

        // Status bar
        layout["StatusBar"].Update(RenderStatusBar());

        AnsiConsole.Write(layout);
    }

    private Panel RenderHeader()
    {
        var grid = new Grid()
            .AddColumn()
            .AddRow(new FigletText("JukeBot").Centered().Color(Color.Cyan1));

        return new Panel(grid)
            .Border(BoxBorder.Double)
            .BorderColor(Color.Cyan1);
    }

    private Panel RenderNavigation(ViewMode currentView)
    {
        var menu = new Rows(
            CreateMenuItem("1", "Library", currentView == ViewMode.Library),
            CreateMenuItem("2", "Artists", currentView == ViewMode.Artists),
            CreateMenuItem("3", "Albums", currentView == ViewMode.Albums),
            CreateMenuItem("4", "Playlists", currentView == ViewMode.Playlists),
            CreateMenuItem("5", "Now Playing", currentView == ViewMode.NowPlaying),
            new Text(""),
            new Markup("[dim]F5[/] Refresh"),
            new Markup("[dim]Q[/] Quit")
        );

        return new Panel(menu)
            .Header("[cyan]Navigation[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue);
    }

    private IRenderable CreateMenuItem(string key, string label, bool selected)
    {
        if (selected)
            return new Markup($"[cyan]► {key}. {label}[/]");
        else
            return new Markup($"[dim]{key}. {label}[/]");
    }

    private Panel RenderMainContent(ViewMode view, int selectedIndex, object items, string? searchQuery)
    {
        var content = view switch
        {
            ViewMode.Library => RenderTrackList((List<Track>)items, selectedIndex),
            ViewMode.Albums => RenderAlbumList((List<Album>)items, selectedIndex),
            ViewMode.Artists => RenderArtistList((List<Artist>)items, selectedIndex),
            ViewMode.Playlists => RenderPlaylistList((List<Playlist>)items, selectedIndex),
            ViewMode.NowPlaying => RenderNowPlaying((List<Track>)items, selectedIndex),
            _ => new Text("Unknown view")
        };

        var title = view switch
        {
            ViewMode.Library => "Library",
            ViewMode.Albums => "Albums",
            ViewMode.Artists => "Artists",
            ViewMode.Playlists => "Playlists",
            ViewMode.NowPlaying => "Now Playing",
            _ => "View"
        };

        return new Panel(content)
            .Header($"[yellow]{title}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Yellow);
    }

    private IRenderable RenderTrackList(List<Track> tracks, int selectedIndex)
    {
        if (tracks.Count == 0)
            return new Markup("[dim]No tracks found. Press F5 to scan your library.[/]");

        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("").Width(2))
            .AddColumn(new TableColumn("Title"))
            .AddColumn(new TableColumn("Artist").Width(25))
            .AddColumn(new TableColumn("Duration").Width(8));

        var displayStart = Math.Max(0, selectedIndex - 10);
        var displayEnd = Math.Min(tracks.Count, displayStart + 20);

        for (int i = displayStart; i < displayEnd; i++)
        {
            var track = tracks[i];
            var marker = i == selectedIndex ? "►" : " ";
            var style = i == selectedIndex ? "cyan" : "white";

            table.AddRow(
                new Markup($"[{style}]{marker}[/]"),
                new Markup($"[{style}]{Markup.Escape(track.Title ?? "Unknown")}[/]"),
                new Markup($"[{style} dim]{Markup.Escape(track.Artist ?? "Unknown")}[/]"),
                new Markup($"[{style} dim]{FormatDuration(track.Duration)}[/]")
            );
        }

        return table;
    }

    private IRenderable RenderAlbumList(List<Album> albums, int selectedIndex)
    {
        if (albums.Count == 0)
            return new Markup("[dim]No albums found.[/]");

        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("").Width(2))
            .AddColumn(new TableColumn("Album"))
            .AddColumn(new TableColumn("Artist").Width(25))
            .AddColumn(new TableColumn("Year").Width(6))
            .AddColumn(new TableColumn("Tracks").Width(8));

        var displayStart = Math.Max(0, selectedIndex - 10);
        var displayEnd = Math.Min(albums.Count, displayStart + 20);

        for (int i = displayStart; i < displayEnd; i++)
        {
            var album = albums[i];
            var marker = i == selectedIndex ? "►" : " ";
            var style = i == selectedIndex ? "cyan" : "white";

            table.AddRow(
                new Markup($"[{style}]{marker}[/]"),
                new Markup($"[{style}]{Markup.Escape(album.Name)}[/]"),
                new Markup($"[{style} dim]{Markup.Escape(album.ArtistName ?? "Unknown")}[/]"),
                new Markup($"[{style} dim]{album.Year?.ToString() ?? ""}[/]"),
                new Markup($"[{style} dim]{album.TrackCount} tracks[/]")
            );
        }

        return table;
    }

    private IRenderable RenderArtistList(List<Artist> artists, int selectedIndex)
    {
        if (artists.Count == 0)
            return new Markup("[dim]No artists found.[/]");

        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("").Width(2))
            .AddColumn(new TableColumn("Artist"))
            .AddColumn(new TableColumn("Albums").Width(10))
            .AddColumn(new TableColumn("Tracks").Width(10));

        var displayStart = Math.Max(0, selectedIndex - 10);
        var displayEnd = Math.Min(artists.Count, displayStart + 20);

        for (int i = displayStart; i < displayEnd; i++)
        {
            var artist = artists[i];
            var marker = i == selectedIndex ? "►" : " ";
            var style = i == selectedIndex ? "cyan" : "white";

            table.AddRow(
                new Markup($"[{style}]{marker}[/]"),
                new Markup($"[{style}]{Markup.Escape(artist.Name)}[/]"),
                new Markup($"[{style} dim]{artist.AlbumCount} albums[/]"),
                new Markup($"[{style} dim]{artist.TrackCount} tracks[/]")
            );
        }

        return table;
    }

    private IRenderable RenderPlaylistList(List<Playlist> playlists, int selectedIndex)
    {
        if (playlists.Count == 0)
            return new Markup("[dim]No playlists. Create one with 'P'.[/]");

        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("").Width(2))
            .AddColumn(new TableColumn("Playlist"))
            .AddColumn(new TableColumn("Tracks").Width(10));

        for (int i = 0; i < playlists.Count; i++)
        {
            var playlist = playlists[i];
            var marker = i == selectedIndex ? "►" : " ";
            var style = i == selectedIndex ? "cyan" : "white";

            table.AddRow(
                new Markup($"[{style}]{marker}[/]"),
                new Markup($"[{style}]{Markup.Escape(playlist.Name)}[/]"),
                new Markup($"[{style} dim]{playlist.Items.Count} tracks[/]")
            );
        }

        return table;
    }

    private IRenderable RenderNowPlaying(List<Track> queue, int selectedIndex)
    {
        var track = _audioBackend.CurrentTrack;

        if (track == null)
            return new Markup("[dim]Nothing playing[/]");

        var grid = new Grid()
            .AddColumn()
            .AddRow(new Markup($"[bold cyan]{Markup.Escape(track.Title ?? "Unknown")}[/]"))
            .AddRow(new Markup($"[yellow]{Markup.Escape(track.Artist ?? "Unknown")}[/]"))
            .AddRow(new Markup($"[dim]{Markup.Escape(track.Album ?? "Unknown")}[/]"))
            .AddRow(new Text(""))
            .AddRow(RenderProgressBar());

        return grid;
    }

    private IRenderable RenderProgressBar()
    {
        var current = _audioBackend.CurrentTime;
        var total = _audioBackend.Duration;

        if (total.TotalSeconds == 0)
            return new Text("");

        var progress = Math.Clamp(current.TotalSeconds / total.TotalSeconds, 0, 1);
        var barWidth = 50;
        var filledWidth = (int)(progress * barWidth);

        // Build ASCII progress bar (use Text to avoid markup parsing)
        var filledBar = new string('█', filledWidth);
        var emptyBar = new string('░', barWidth - filledWidth);
        var bar = $"[{filledBar}{emptyBar}]";

        var timeLabel = $"{FormatDuration(current)} / {FormatDuration(total)}";

        var grid = new Grid();
        grid.AddColumn();
        grid.AddRow(new Text(bar, new Style(Color.Cyan1)));
        grid.AddRow(new Text(timeLabel, new Style(Color.Grey)).Centered());

        return grid;
    }

    private Panel RenderVisualizer()
    {
        var spectrum = _lastSpectrum.Length > 0 ? _lastSpectrum : new float[24];
        var content = RenderSpectrumBars(spectrum);

        return new Panel(content)
            .Header("[green]Visualizer[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green);
    }

    private IRenderable RenderSpectrumBars(float[] spectrum)
    {
        if (!_audioBackend.IsPlaying || spectrum.Length == 0)
            return new Markup("[dim]No audio playing[/]");

        // Use text-based bars for real-time spectrum display
        var bars = new Grid();
        bars.AddColumn(new GridColumn().Width(28));

        const int maxHeight = 20;
        var barChars = new[] { "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█" };

        // Build each row from top to bottom
        for (int row = maxHeight - 1; row >= 0; row--)
        {
            var line = "";
            for (int i = 0; i < spectrum.Length; i++)
            {
                var barHeight = (int)(spectrum[i] * maxHeight);

                if (barHeight > row)
                {
                    // Determine color based on height
                    var color = barHeight > maxHeight * 0.8 ? "red" :
                               barHeight > maxHeight * 0.5 ? "yellow" :
                               "green";

                    // Use full block if well above this row, otherwise use partial
                    var charIndex = Math.Min(7, Math.Max(0, (barHeight - row) * 8 / 2));
                    line += $"[{color}]{barChars[Math.Min(7, charIndex)]}[/]";
                }
                else
                {
                    line += " ";
                }
            }
            bars.AddRow(new Markup(line));
        }

        return bars;
    }

    private Panel RenderStatusBar()
    {
        var controls = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("").Width(30))
            .AddColumn(new TableColumn("").Width(30))
            .AddColumn(new TableColumn(""));

        var playback = _audioBackend.IsPlaying ? "▶ Playing" :
                      _audioBackend.IsPaused ? "⏸ Paused" :
                      "⏹ Stopped";

        var volume = _audioBackend.Muted ? "🔇 Muted" :
                    $"🔊 {_audioBackend.Volume}%";

        controls.AddRow(
            new Markup($"[cyan]{playback}[/]"),
            new Markup("[dim]Space[/] Play/Pause  [dim]N[/] Next  [dim]←→[/] Seek  [dim]±[/] Volume"),
            new Markup($"[yellow]{volume}[/]")
        );

        return new Panel(controls)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey);
    }

    private string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return duration.ToString(@"h\:mm\:ss");
        return duration.ToString(@"m\:ss");
    }
}
