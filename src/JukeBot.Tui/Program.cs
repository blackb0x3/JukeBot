using JukeBot.Core.Audio;
using JukeBot.Core.Data;
using JukeBot.Core.Services;
using JukeBot.Tui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

// Set up dependency injection
var services = new ServiceCollection();

// Logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Warning);
});

// Database
var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "JukeBot",
    "jukebot.db"
);
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

services.AddSingleton<IRepository>(sp =>
    new LiteDbRepository(dbPath, sp.GetRequiredService<ILogger<LiteDbRepository>>()));

// Audio backend
services.AddSingleton<IAudioBackend>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<LibVlcAudioBackend>>();
    try
    {
        return new LibVlcAudioBackend(logger);
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Failed to initialize LibVLC backend:[/] {Markup.Escape(ex.Message)}");
        AnsiConsole.MarkupLine("[yellow]Attempting to use NAudio fallback (Windows only)...[/]");

        if (OperatingSystem.IsWindows())
        {
            var naudioLogger = sp.GetRequiredService<ILogger<NaudioBackend>>();
            return new NaudioBackend(naudioLogger);
        }
        else
        {
            AnsiConsole.MarkupLine("[red]NAudio is only available on Windows.[/]");
            AnsiConsole.MarkupLine("[yellow]Please install VLC/LibVLC for your platform:[/]");
            AnsiConsole.MarkupLine("  Windows: Install VLC or use NuGet package VideoLAN.LibVLC.Windows");
            AnsiConsole.MarkupLine("  macOS: brew install --cask vlc");
            AnsiConsole.MarkupLine("  Linux: sudo apt install vlc (or your distro's package manager)");
            Environment.Exit(1);
            throw;
        }
    }
});

// Services
services.AddSingleton<LibraryScanner>();
services.AddSingleton<PlaylistService>();
services.AddSingleton<SearchService>();
services.AddSingleton(sp =>
{
    var repo = sp.GetRequiredService<IRepository>();
    var settings = repo.GetSettings();
    var logger = sp.GetRequiredService<ILogger<AudioAnalysisService>>();
    return new AudioAnalysisService(settings.SpectrumBins, logger);
});

// Application
services.AddSingleton<JukeBotApp>();

var serviceProvider = services.BuildServiceProvider();

try
{
    var app = serviceProvider.GetRequiredService<JukeBotApp>();
    await app.RunAsync();
    app.Dispose();
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex);
}
finally
{
    await serviceProvider.DisposeAsync();
}
