using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;

namespace NotifierRedirecter.Events;

public sealed class GuildDownloadCompletedEventHandler
{
    private static readonly ILogger<GuildDownloadCompletedEventHandler> Logger = Program.LoggerFactory.CreateLogger<GuildDownloadCompletedEventHandler>();

    public static Task ExecuteAsync(DiscordClient _, GuildDownloadCompletedEventArgs eventArgs)
    {
        foreach (DiscordGuild guild in eventArgs.Guilds.Values)
        {
            Logger.LogDebug("Guild ({GuildId}) is available with {MemberCount:N0} members.", guild.Id, guild.MemberCount);
        }

        Logger.LogInformation("{GuildCount:N0} guilds are ready to go!", eventArgs.Guilds.Count);
        return Task.CompletedTask;
    }
}
