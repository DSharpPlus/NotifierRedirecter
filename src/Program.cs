using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DSharpPlus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotifierRedirecter.Events;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace NotifierRedirecter;

public sealed class Program
{
    internal static readonly IConfiguration Configuration;
    internal static readonly Database Database;
    private static readonly ILogger<Program> Logger;

    static Program()
    {
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("config.json", true, true)
            .AddEnvironmentVariables("NOTIFIER_REDIRECTER_")
            .Build();

        const string loggingFormat = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u4}] {SourceContext}: {Message:lj}{NewLine}{Exception}";
        LoggerConfiguration configuration = new LoggerConfiguration()
            .MinimumLevel.Is(Configuration.GetValue("Logging:Level", LogEventLevel.Debug))
            .WriteTo.Console(outputTemplate: loggingFormat, formatProvider: CultureInfo.InvariantCulture, theme: new AnsiConsoleTheme(new Dictionary<ConsoleThemeStyle, string>
            {
                [ConsoleThemeStyle.Text] = "\x1b[0m",
                [ConsoleThemeStyle.SecondaryText] = "\x1b[90m",
                [ConsoleThemeStyle.TertiaryText] = "\x1b[90m",
                [ConsoleThemeStyle.Invalid] = "\x1b[31m",
                [ConsoleThemeStyle.Null] = "\x1b[95m",
                [ConsoleThemeStyle.Name] = "\x1b[93m",
                [ConsoleThemeStyle.String] = "\x1b[96m",
                [ConsoleThemeStyle.Number] = "\x1b[95m",
                [ConsoleThemeStyle.Boolean] = "\x1b[95m",
                [ConsoleThemeStyle.Scalar] = "\x1b[95m",
                [ConsoleThemeStyle.LevelVerbose] = "\x1b[34m",
                [ConsoleThemeStyle.LevelDebug] = "\x1b[90m",
                [ConsoleThemeStyle.LevelInformation] = "\x1b[36m",
                [ConsoleThemeStyle.LevelWarning] = "\x1b[33m",
                [ConsoleThemeStyle.LevelError] = "\x1b[31m",
                [ConsoleThemeStyle.LevelFatal] = "\x1b[97;91m"
            }))
            .WriteTo.File(
                $"logs/{DateTime.Now.ToUniversalTime().ToString("yyyy'-'MM'-'dd' 'HH'.'mm'.'ss", CultureInfo.InvariantCulture)}.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: loggingFormat,
                formatProvider: CultureInfo.InvariantCulture
            );

        Log.Logger = configuration.CreateLogger();
        Logger = (ILogger<Program>)Log.Logger.ForContext<Program>();

        if (Configuration.GetValue<string>("Token") is null)
        {
            Logger.LogCritical("Discord token parameter is required but not found.");
            Environment.Exit(1);
        }
        else if (Configuration.GetConnectionString("Database") is null)
        {
            Logger.LogCritical("Database connection string parameter is required but not found.");
            Environment.Exit(1);
        }

        Database = new(Configuration);
    }

    public static Task Main()
    {
        DiscordClient client = new(new DiscordConfiguration()
        {
            Token = Configuration.GetValue<string>("Token")!,
            Intents = DiscordIntents.Guilds | DiscordIntents.GuildMessages | DiscordIntents.MessageContents,
            LoggerFactory = (ILoggerFactory)Logger,
            LogUnknownEvents = false
        });

        client.MessageCreated += MessageCreatedEventHandler.ExecuteAsync;
        client.GuildDownloadCompleted += GuildDownloadCompletedEventHandler.ExecuteAsync;
        Logger.LogInformation("Connecting to Discord...");
        return Database.InitializeAsync().ContinueWith(_ => client.ConnectAsync()).ContinueWith(_ => Task.Delay(-1));
    }
}
