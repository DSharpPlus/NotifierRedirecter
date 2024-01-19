using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace NotifierRedirecter.Events;

public sealed class GuildDownloadCompletedEventHandler(ILogger<GuildDownloadCompletedEventHandler>? logger = null)
{
    private readonly ILogger<GuildDownloadCompletedEventHandler> _logger = logger ?? NullLogger<GuildDownloadCompletedEventHandler>.Instance;

    public Task ExecuteAsync(DiscordClient _, GuildDownloadCompletedEventArgs eventArgs)
    {
        foreach (DiscordGuild guild in eventArgs.Guilds.Values)
        {
            this._logger.LogDebug("Guild ({GuildId}) is available with {MemberCount:N0} members.", guild.Id, guild.MemberCount);
        }

        this._logger.LogInformation("{GuildCount:N0} guilds are ready to go!", eventArgs.Guilds.Count);
        return Task.CompletedTask;
    }
}
