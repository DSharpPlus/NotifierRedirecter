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
using NotifierRedirecter.Events.Handlers;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace NotifierRedirecter;

public sealed class Program
{
    internal static IServiceProvider ServiceProvider = null!;

    public static async Task Main(string[] args)
    {
        ConfigurationBuilder configurationBuilder = new();
        configurationBuilder.Sources.Clear();
        configurationBuilder.AddJsonFile("config.json", true, true);
#if DEBUG
        configurationBuilder.AddJsonFile("config.debug.json", true, true);
#endif
        configurationBuilder.AddEnvironmentVariables("NOTIFIER_REDIRECTER__");
        configurationBuilder.AddCommandLine(args);

        IConfiguration configuration = configurationBuilder.Build();
        ServiceCollection serviceCollection = new();
        serviceCollection.AddSingleton(configuration);
        serviceCollection.AddLogging(logger =>
        {
            string? loggingFormat = configuration.GetValue<string?>("Logging:Format");
            if (string.IsNullOrWhiteSpace(loggingFormat))
            {
                loggingFormat = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u4}] {SourceContext}: {Message:lj}{NewLine}{Exception}";
            }

            string? filename = configuration.GetValue<string?>("Logging:Filename");
            if (!string.IsNullOrWhiteSpace(filename))
            {
                filename = "yyyy'-'MM'-'dd' 'HH'.'mm'.'ss";
            }

            // Log both to console and the file
            LoggerConfiguration loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(configuration.GetValue("Logging:Level", LogEventLevel.Debug))
            .WriteTo.Console(
                formatProvider: CultureInfo.InvariantCulture,
                outputTemplate: loggingFormat,
                theme: new AnsiConsoleTheme(new Dictionary<ConsoleThemeStyle, string>
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
                $"logs/{DateTime.Now.ToUniversalTime().ToString(filename, CultureInfo.InvariantCulture)}-.log",
                formatProvider: CultureInfo.InvariantCulture,
                outputTemplate: loggingFormat,
                rollingInterval: RollingInterval.Day
            );

            // Allow specific namespace log level overrides, which allows us to hush output from things like the database basic SELECT queries on the Information level.
            foreach (IConfigurationSection logOverride in configuration.GetSection("logging:overrides").GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(logOverride.Value) && Enum.TryParse(logOverride.Value, out LogEventLevel logEventLevel))
                {
                    loggerConfiguration.MinimumLevel.Override(logOverride.Key, logEventLevel);
                }
            }

            logger.AddSerilog(loggerConfiguration.CreateLogger());
        });

        serviceCollection.AddSingleton<Database>();
        serviceCollection.AddSingleton<UserActivityTracker>();
        serviceCollection.AddSingleton<GuildDownloadCompletedEventHandler>();
        serviceCollection.AddSingleton<MessageCreatedEventHandler>();
        serviceCollection.AddSingleton<TypingStartedEventHandler>();

        // Register the Discord sharded client to the service collection
        serviceCollection.AddSingleton((serviceProvider) =>
        {
            ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
            string? discordToken = configuration.GetValue<string>("discord:token");
            if (string.IsNullOrWhiteSpace(discordToken))
            {
                logger.LogCritical("Discord:Token was not provided to the application. Please provide a token in the configuration file, through an environment variable, or as a command line argument.");
                Environment.Exit(1);
            }

            DiscordClient client = new(new DiscordConfiguration()
            {
                Token = discordToken,
                Intents = DiscordIntents.Guilds | DiscordIntents.GuildMessages | DiscordIntents.MessageContents,
                LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>(),
                LogUnknownEvents = false
            });

            CommandAllExtension commandAll = client.UseCommandAll(new CommandAllConfiguration(serviceCollection)
            {
#if DEBUG
                DebugGuildId = configuration.GetValue<ulong>("discord:debug_guild_id"),
#endif
                PrefixParser = new PrefixParser(configuration.GetSection("discord:prefixes").Get<string[]>() ?? ["n!"])
            });

            commandAll.AddCommands(typeof(Program).Assembly);
            commandAll.CommandErrored += CommandErroredEventHandler.ExecuteAsync;
            client.GuildDownloadCompleted += serviceProvider.GetRequiredService<GuildDownloadCompletedEventHandler>().ExecuteAsync;
            client.MessageCreated += serviceProvider.GetRequiredService<MessageCreatedEventHandler>().ExecuteAsync;
            client.TypingStarted += serviceProvider.GetRequiredService<TypingStartedEventHandler>().ExecuteAsync;

            return client;
        });

        ServiceProvider = serviceCollection.BuildServiceProvider();
        DiscordClient client = ServiceProvider.GetRequiredService<DiscordClient>();
        await client.ConnectAsync();
        await Task.Delay(-1);
    }
}
