using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotifierRedirecter.Configuration;
using NotifierRedirecter.Events;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using DSharpPlusDiscordConfiguration = DSharpPlus.DiscordConfiguration;
using SerilogLoggerConfiguration = Serilog.LoggerConfiguration;

namespace NotifierRedirecter;

public sealed class Program
{
    public static async Task Main(string[] args)
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(serviceProvider =>
        {
            ConfigurationBuilder configurationBuilder = new();
            configurationBuilder.Sources.Clear();
            configurationBuilder.AddJsonFile("config.json", true, true);
#if DEBUG
            configurationBuilder.AddJsonFile("config.debug.json", true, true);
#endif
            configurationBuilder.AddEnvironmentVariables("NotifierRedirecter__");
            configurationBuilder.AddCommandLine(args);

            IConfiguration configuration = configurationBuilder.Build();
            NotifierConfiguration? notifierConfiguration = configuration.Get<NotifierConfiguration>();
            if (notifierConfiguration is null)
            {
                Console.WriteLine("No configuration found! Please modify the config file, set environment variables or pass command line arguments. Exiting...");
                Environment.Exit(1);
            }

            return notifierConfiguration;
        });

        services.AddLogging(logging =>
        {
            IServiceProvider serviceProvider = logging.Services.BuildServiceProvider();
            NotifierConfiguration notifierConfiguration = serviceProvider.GetRequiredService<NotifierConfiguration>();
            SerilogLoggerConfiguration serilogLoggerConfiguration = new();
            serilogLoggerConfiguration.MinimumLevel.Is(notifierConfiguration.Logger.LogLevel);
            serilogLoggerConfiguration.WriteTo.Console(
                formatProvider: CultureInfo.InvariantCulture,
                outputTemplate: notifierConfiguration.Logger.Format,
                theme: AnsiConsoleTheme.Code
            );

            serilogLoggerConfiguration.WriteTo.File(
                formatProvider: CultureInfo.InvariantCulture,
                path: $"{notifierConfiguration.Logger.Path}/{notifierConfiguration.Logger.FileName}.log",
                rollingInterval: notifierConfiguration.Logger.RollingInterval,
                outputTemplate: notifierConfiguration.Logger.Format
            );

            // Sometimes the user/dev needs more or less information about a speific part of the bot
            // so we allow them to override the log level for a specific namespace.
            if (notifierConfiguration.Logger.Overrides.Count > 0)
            {
                foreach ((string key, LogEventLevel value) in notifierConfiguration.Logger.Overrides)
                {
                    serilogLoggerConfiguration.MinimumLevel.Override(key, value);
                }
            }

            logging.AddSerilog(serilogLoggerConfiguration.CreateLogger());
        });

        services.AddSingleton<Database>();
        services.AddSingleton<UserActivityTracker>();
        services.AddSingleton((serviceProvider) =>
        {
            DiscordEventManager eventManager = new(serviceProvider);
            eventManager.GatherEventHandlers(typeof(Program).Assembly);
            return eventManager;
        });

        services.AddSingleton(serviceProvider =>
        {
            NotifierConfiguration notifierConfiguration = serviceProvider.GetRequiredService<NotifierConfiguration>();
            if (notifierConfiguration.Discord is null || string.IsNullOrWhiteSpace(notifierConfiguration.Discord.Token))
            {
                serviceProvider.GetRequiredService<ILogger<Program>>().LogCritical("Discord token is not set! Exiting...");
                Environment.Exit(1);
            }

            DiscordShardedClient discordClient = new(new DSharpPlusDiscordConfiguration
            {
                Token = notifierConfiguration.Discord.Token,
                Intents = TextCommandProcessor.RequiredIntents | SlashCommandProcessor.RequiredIntents | DiscordIntents.GuildVoiceStates | DiscordIntents.MessageContents,
                LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>(),
            });

            return discordClient;
        });

        // Almost start the program
        IServiceProvider serviceProvider = services.BuildServiceProvider();
        NotifierConfiguration notifierConfiguration = serviceProvider.GetRequiredService<NotifierConfiguration>();
        DiscordShardedClient discordClient = serviceProvider.GetRequiredService<DiscordShardedClient>();
        DiscordEventManager eventManager = serviceProvider.GetRequiredService<DiscordEventManager>();

        // Register extensions here since these involve asynchronous operations
        IReadOnlyDictionary<int, CommandsExtension> commandsExtensions = await discordClient.UseCommandsAsync(new CommandsConfiguration()
        {
            ServiceProvider = serviceProvider,
            DebugGuildId = notifierConfiguration.Discord.GuildId
        });

        // Iterate through each Discord shard
        foreach (CommandsExtension commandsExtension in commandsExtensions.Values)
        {
            // Add all commands by scanning the current assembly
            commandsExtension.AddCommands(typeof(Program).Assembly);
            TextCommandProcessor textCommandProcessor = new(new()
            {
                PrefixResolver = new DefaultPrefixResolver(notifierConfiguration.Discord.Prefix).ResolvePrefixAsync
            });

            // Add text commands (h!ping) and slash commands (/ping)
            await commandsExtension.AddProcessorsAsync(textCommandProcessor, new SlashCommandProcessor());
            eventManager.RegisterEventHandlers(commandsExtension);
        }

        eventManager.RegisterEventHandlers(discordClient);
        await discordClient.StartAsync();
        await Task.Delay(-1);
    }
}
