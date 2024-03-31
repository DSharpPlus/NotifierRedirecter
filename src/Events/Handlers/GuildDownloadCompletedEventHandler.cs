using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace NotifierRedirecter.Events.Handlers;

public sealed class GuildDownloadCompletedEventHandler
{
    private readonly ILogger<GuildDownloadCompletedEventHandler> _logger;
    public GuildDownloadCompletedEventHandler(ILogger<GuildDownloadCompletedEventHandler>? logger = null) => this._logger = logger ?? NullLogger<GuildDownloadCompletedEventHandler>.Instance;

    [DiscordEvent(DiscordIntents.Guilds)]
    public Task ExecuteAsync(DiscordClient client, GuildDownloadCompletedEventArgs eventArgs)
    {
        foreach (DiscordGuild guild in eventArgs.Guilds.Values)
        {
            this._logger.LogDebug("Guild ({GuildId}) is available with {MemberCount:N0} members.", guild.Id, guild.MemberCount);
        }

        this._logger.LogInformation("{GuildCount:N0} guilds are ready to go!", eventArgs.Guilds.Count);
        return client.UpdateStatusAsync(new DiscordActivity("Messing around with your notifications...", ActivityType.Custom));
    }
}
