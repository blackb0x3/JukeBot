using JukeBot.Core.Audio;
using JukeBot.Core.Data;
using JukeBot.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

if (args.Length == 0)
{
    ShowHelp();
    return;
}

var command = args[0].ToLowerInvariant();
var commandArgs = args.Skip(1).ToArray();

// Set up DI
var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "JukeBot",
    "jukebot.db"
);
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

services.AddSingleton<IRepository>(sp =>
    new LiteDbRepository(dbPath, sp.GetRequiredService<ILogger<LiteDbRepository>>()));

services.AddSingleton<LibraryScanner>();
services.AddSingleton<PlaylistService>();
services.AddSingleton<SearchService>();

var serviceProvider = services.BuildServiceProvider();

try
{
    await ExecuteCommandAsync(command, commandArgs, serviceProvider);
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
    Environment.ExitCode = 1;
}
finally
{
    await serviceProvider.DisposeAsync();
}

async Task ExecuteCommandAsync(string cmd, string[] cmdArgs, ServiceProvider sp)
{
    switch (cmd)
    {
        case "scan":
            await ScanCommand(cmdArgs, sp);
            break;

        case "play":
            await PlayCommand(cmdArgs, sp);
            break;

        case "queue":
            await QueueCommand(cmdArgs, sp);
            break;

        case "playlist":
            await PlaylistCommand(cmdArgs, sp);
            break;

        case "import":
            await ImportCommand(cmdArgs, sp);
            break;

        case "export":
            await ExportCommand(cmdArgs, sp);
            break;

        case "config":
            await ConfigCommand(cmdArgs, sp);
            break;

        case "search":
            await SearchCommand(cmdArgs, sp);
            break;

        case "stats":
            await StatsCommand(sp);
            break;

        case "help":
        case "--help":
        case "-h":
            ShowHelp();
            break;

        default:
            AnsiConsole.MarkupLine($"[red]Unknown command:[/] {cmd}");
            ShowHelp();
            break;
    }
}

async Task ScanCommand(string[] cmdArgs, ServiceProvider sp)
{
    if (cmdArgs.Length == 0)
    {
        AnsiConsole.MarkupLine("[red]Usage:[/] jukebot scan <path>");
        return;
    }

    var path = cmdArgs[0];
    if (!Directory.Exists(path))
    {
        AnsiConsole.MarkupLine($"[red]Directory not found:[/] {path}");
        return;
    }

    var scanner = sp.GetRequiredService<LibraryScanner>();
    var repo = sp.GetRequiredService<IRepository>();

    AnsiConsole.MarkupLine($"[cyan]Scanning:[/] {path}");

    await AnsiConsole.Progress()
        .AutoClear(false)
        .Columns(
            new TaskDescriptionColumn(),
            new ProgressBarColumn(),
            new PercentageColumn(),
            new SpinnerColumn())
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask("[green]Scanning files[/]");

            scanner.ProgressChanged += (s, e) =>
            {
                task.MaxValue = e.TotalFiles;
                task.Value = e.ProcessedFiles;
                task.Description = $"[green]{e.ProcessedFiles}/{e.TotalFiles}[/] ({e.AddedCount} added)";
            };

            await scanner.ScanDirectoryAsync(path);
        });

    AnsiConsole.MarkupLine("[green]Scan completed![/]");
}

async Task PlayCommand(string[] cmdArgs, ServiceProvider sp)
{
    AnsiConsole.MarkupLine("[yellow]Play command not supported in CLI mode. Use the TUI application.[/]");
    await Task.CompletedTask;
}

async Task QueueCommand(string[] cmdArgs, ServiceProvider sp)
{
    AnsiConsole.MarkupLine("[yellow]Queue command not supported in CLI mode. Use the TUI application.[/]");
    await Task.CompletedTask;
}

async Task PlaylistCommand(string[] cmdArgs, ServiceProvider sp)
{
    if (cmdArgs.Length == 0)
    {
        AnsiConsole.MarkupLine("[red]Usage:[/] jukebot playlist <create|add|remove|list> [args]");
        return;
    }

    var playlistService = sp.GetRequiredService<PlaylistService>();
    var subCommand = cmdArgs[0].ToLowerInvariant();

    switch (subCommand)
    {
        case "list":
            var playlists = playlistService.GetAllPlaylists();
            if (playlists.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]No playlists found.[/]");
                return;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Name")
                .AddColumn("Tracks")
                .AddColumn("Created");

            foreach (var playlist in playlists)
            {
                table.AddRow(
                    playlist.Name,
                    playlist.Items.Count.ToString(),
                    playlist.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd")
                );
            }

            AnsiConsole.Write(table);
            break;

        case "create":
            if (cmdArgs.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] jukebot playlist create <name>");
                return;
            }

            var name = cmdArgs[1];
            playlistService.CreatePlaylist(name);
            AnsiConsole.MarkupLine($"[green]Created playlist:[/] {name}");
            break;

        default:
            AnsiConsole.MarkupLine($"[red]Unknown playlist command:[/] {subCommand}");
            break;
    }

    await Task.CompletedTask;
}

async Task ImportCommand(string[] cmdArgs, ServiceProvider sp)
{
    if (cmdArgs.Length < 2)
    {
        AnsiConsole.MarkupLine("[red]Usage:[/] jukebot import <m3u-file> <playlist-name>");
        return;
    }

    var filePath = cmdArgs[0];
    var playlistName = cmdArgs[1];

    if (!File.Exists(filePath))
    {
        AnsiConsole.MarkupLine($"[red]File not found:[/] {filePath}");
        return;
    }

    var playlistService = sp.GetRequiredService<PlaylistService>();
    playlistService.ImportM3U(filePath, playlistName);

    AnsiConsole.MarkupLine($"[green]Imported playlist:[/] {playlistName}");
    await Task.CompletedTask;
}

async Task ExportCommand(string[] cmdArgs, ServiceProvider sp)
{
    if (cmdArgs.Length < 2)
    {
        AnsiConsole.MarkupLine("[red]Usage:[/] jukebot export <playlist-name> <output-file>");
        return;
    }

    var playlistName = cmdArgs[0];
    var outputFile = cmdArgs[1];

    var playlistService = sp.GetRequiredService<PlaylistService>();
    var playlists = playlistService.GetAllPlaylists();
    var playlist = playlists.FirstOrDefault(p => p.Name.Equals(playlistName, StringComparison.OrdinalIgnoreCase));

    if (playlist == null)
    {
        AnsiConsole.MarkupLine($"[red]Playlist not found:[/] {playlistName}");
        return;
    }

    playlistService.ExportM3U(playlist.Id, outputFile);
    AnsiConsole.MarkupLine($"[green]Exported playlist to:[/] {outputFile}");
    await Task.CompletedTask;
}

async Task ConfigCommand(string[] cmdArgs, ServiceProvider sp)
{
    var repo = sp.GetRequiredService<IRepository>();
    var settings = repo.GetSettings();

    if (cmdArgs.Length == 0)
    {
        // Show all settings
        AnsiConsole.MarkupLine("[cyan]Current configuration:[/]");
        AnsiConsole.MarkupLine($"  Volume: {settings.Volume}");
        AnsiConsole.MarkupLine($"  Muted: {settings.Muted}");
        AnsiConsole.MarkupLine($"  Repeat: {settings.RepeatMode}");
        AnsiConsole.MarkupLine($"  Shuffle: {settings.Shuffle}");
        AnsiConsole.MarkupLine($"  Audio Backend: {settings.AudioBackend}");
        AnsiConsole.MarkupLine($"  Library Paths: {string.Join(", ", settings.LibraryPaths)}");
        return;
    }

    var action = cmdArgs[0].ToLowerInvariant();

    if (action == "set" && cmdArgs.Length >= 3)
    {
        var key = cmdArgs[1].ToLowerInvariant();
        var value = cmdArgs[2];

        switch (key)
        {
            case "volume":
                settings.Volume = int.Parse(value);
                break;
            case "backend":
                settings.AudioBackend = value;
                break;
            case "library-path":
                if (!settings.LibraryPaths.Contains(value))
                    settings.LibraryPaths.Add(value);
                break;
            default:
                AnsiConsole.MarkupLine($"[red]Unknown setting:[/] {key}");
                return;
        }

        repo.SaveSettings(settings);
        AnsiConsole.MarkupLine($"[green]Setting updated:[/] {key} = {value}");
    }
    else if (action == "get" && cmdArgs.Length >= 2)
    {
        var key = cmdArgs[1].ToLowerInvariant();
        switch (key)
        {
            case "volume":
                AnsiConsole.MarkupLine($"Volume: {settings.Volume}");
                break;
            case "backend":
                AnsiConsole.MarkupLine($"Audio Backend: {settings.AudioBackend}");
                break;
            default:
                AnsiConsole.MarkupLine($"[red]Unknown setting:[/] {key}");
                break;
        }
    }
    else
    {
        AnsiConsole.MarkupLine("[red]Usage:[/] jukebot config [set|get] <key> [value]");
    }

    await Task.CompletedTask;
}

async Task SearchCommand(string[] cmdArgs, ServiceProvider sp)
{
    if (cmdArgs.Length == 0)
    {
        AnsiConsole.MarkupLine("[red]Usage:[/] jukebot search <query>");
        return;
    }

    var query = string.Join(" ", cmdArgs);
    var searchService = sp.GetRequiredService<SearchService>();
    var results = searchService.Search(query);

    if (results.TotalCount == 0)
    {
        AnsiConsole.MarkupLine("[dim]No results found.[/]");
        return;
    }

    AnsiConsole.MarkupLine($"[cyan]Found {results.TotalCount} results for:[/] {query}");
    AnsiConsole.WriteLine();

    if (results.Tracks.Count > 0)
    {
        AnsiConsole.MarkupLine($"[yellow]Tracks ({results.Tracks.Count}):[/]");
        foreach (var track in results.Tracks.Take(10))
        {
            AnsiConsole.MarkupLine($"  • {track.Title} - {track.Artist}");
        }
        AnsiConsole.WriteLine();
    }

    if (results.Albums.Count > 0)
    {
        AnsiConsole.MarkupLine($"[yellow]Albums ({results.Albums.Count}):[/]");
        foreach (var album in results.Albums.Take(10))
        {
            AnsiConsole.MarkupLine($"  • {album.Name} - {album.ArtistName}");
        }
        AnsiConsole.WriteLine();
    }

    if (results.Artists.Count > 0)
    {
        AnsiConsole.MarkupLine($"[yellow]Artists ({results.Artists.Count}):[/]");
        foreach (var artist in results.Artists.Take(10))
        {
            AnsiConsole.MarkupLine($"  • {artist.Name}");
        }
    }

    await Task.CompletedTask;
}

async Task StatsCommand(ServiceProvider sp)
{
    var repo = sp.GetRequiredService<IRepository>();

    var trackCount = repo.GetTrackCount();
    var albumCount = repo.GetAllAlbums().Count;
    var artistCount = repo.GetAllArtists().Count;
    var playlistCount = repo.GetAllPlaylists().Count;

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Statistic")
        .AddColumn("Count");

    table.AddRow("Tracks", trackCount.ToString());
    table.AddRow("Albums", albumCount.ToString());
    table.AddRow("Artists", artistCount.ToString());
    table.AddRow("Playlists", playlistCount.ToString());

    AnsiConsole.Write(table);
    await Task.CompletedTask;
}

void ShowHelp()
{
    AnsiConsole.Write(new FigletText("JukeBot CLI").Centered().Color(Color.Cyan1));
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("[cyan]Usage:[/] jukebot <command> [args]");
    AnsiConsole.WriteLine();

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Command")
        .AddColumn("Description");

    table.AddRow("scan <path>", "Scan a directory for music files");
    table.AddRow("playlist list", "List all playlists");
    table.AddRow("playlist create <name>", "Create a new playlist");
    table.AddRow("import <file> <name>", "Import M3U playlist");
    table.AddRow("export <name> <file>", "Export playlist to M3U");
    table.AddRow("search <query>", "Search library");
    table.AddRow("stats", "Show library statistics");
    table.AddRow("config [get|set] [key] [value]", "View or modify configuration");
    table.AddRow("help", "Show this help message");

    AnsiConsole.Write(table);
}
