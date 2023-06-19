using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandAll;
using DSharpPlus.CommandAll.Parsers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotifierRedirecter.Events;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace NotifierRedirecter;

public sealed class Program
{
    internal static readonly IServiceCollection Services;
    internal static readonly IConfiguration Configuration;
    internal static readonly Database Database;
    internal static readonly ILoggerFactory LoggerFactory;
    private static readonly ILogger<Program> Logger;

    static Program()
    {
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("config.json", true, true)
            .AddEnvironmentVariables("NOTIFIER_REDIRECTER_")
            .Build();

        const string loggingFormat = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u4}] {SourceContext}: {Message:lj}{NewLine}{Exception}";
        LoggerConfiguration loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(Configuration.GetValue("logging:level", LogEventLevel.Debug))
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

        // Allow specific namespace log level overrides, which allows us to hush output from things like the database basic SELECT queries on the Information level.
        foreach (IConfigurationSection logOverride in Configuration.GetSection("logging:overrides").GetChildren())
        {
            if (logOverride.Value is null || !Enum.TryParse(logOverride.Value, out LogEventLevel logEventLevel))
            {
                continue;
            }

            loggerConfiguration.MinimumLevel.Override(logOverride.Key, logEventLevel);
        }

        Services = new ServiceCollection().AddLogging(builder => builder.AddSerilog(loggerConfiguration.CreateLogger()));
        LoggerFactory = Services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
        Logger = LoggerFactory.CreateLogger<Program>();

        if (Configuration.GetValue<string>("discord:token") is null)
        {
            Logger.LogCritical("Discord token parameter is required but not found.");
            Environment.Exit(1);
        }
#if DEBUG
        else if (Configuration.GetValue<ulong>("discord:debug_guild_id") is 0)
        {
            Logger.LogCritical("Debug guild ID parameter is required but not found.");
            Environment.Exit(1);
        }
#endif

        Database = new(Configuration);
    }

    public static async Task Main()
    {
        DiscordClient client = new(new DiscordConfiguration()
        {
            Token = Configuration.GetValue<string>("discord:token")!,
            Intents = DiscordIntents.Guilds | DiscordIntents.GuildMessages | DiscordIntents.MessageContents,
            LoggerFactory = LoggerFactory,
            LogUnknownEvents = false
        });

        CommandAllExtension commandAll = client.UseCommandAll(new CommandAllConfiguration(Services)
        {
            DebugGuildId = Configuration.GetValue<ulong>("discord:debug_guild_id"),
            PrefixParser = new PrefixParser(Configuration.GetSection("discord:prefixes").Get<string[]>() ?? new[] { "n!" })
        });
        commandAll.AddCommands(typeof(Program).Assembly);
        commandAll.CommandErrored += CommandErroredEventHandler.ExecuteAsync;

        client.MessageCreated += MessageCreatedEventHandler.ExecuteAsync;
        client.GuildDownloadCompleted += GuildDownloadCompletedEventHandler.ExecuteAsync;
        Logger.LogInformation("Connecting to Discord...");
        await client.ConnectAsync();
        await Task.Delay(-1);
    }
}
