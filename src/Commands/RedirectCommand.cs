using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Commands.Trees.Attributes;
using DSharpPlus.Entities;

namespace NotifierRedirecter.Commands;

[Command("redirect"), Description("Manages which channels to start redirecting notifications for."), RequireGuild]
public sealed class RedirectCommand
{
    private readonly Database _database;
    public RedirectCommand(Database database) => this._database = database;

    [Command("add"), Description("Start redirecting pings from this channel into user's DMs.")]
    [RequirePermissions(Permissions.None, Permissions.ManageMessages)]
    public ValueTask AddAsync(CommandContext context, [Description("Which channel to start listening for pings.")] DiscordChannel channel)
    {
        if (this._database.IsRedirect(channel.Id))
        {
            return context.RespondAsync($"I'm already redirecting notifications from {channel.Mention}.");
        }

        this._database.AddRedirect(context.Guild!.Id, channel.Id);
        return context.RespondAsync($"I've added {channel.Mention} to the redirect list.");
    }

    [Command("remove"), Description("Stop redirecting notifications from this channel into user's DMs.")]
    [RequirePermissions(Permissions.None, Permissions.ManageMessages)]
    public ValueTask RemoveAsync(CommandContext context, [Description("Which channel to stop listening for pings.")] DiscordChannel channel)
    {
        if (!this._database.IsRedirect(channel.Id))
        {
            return context.RespondAsync($"I'm currently not redirecting notifications from {channel.Mention}.");
        }

        this._database.RemoveRedirect(context.Guild!.Id, channel.Id);
        return context.RespondAsync($"I've removed {channel.Mention} from the redirect list. User's will no longer receive pings from that channel.");
    }

    [Command("list"), Description("List all channels I'm redirecting into user's DMs.")]
    public ValueTask ListAsync(CommandContext context)
    {
        IReadOnlyList<ulong> redirectedChannels = this._database.ListRedirects(context.Guild!.Id);
        return redirectedChannels.Count switch
        {
            0 => context.RespondAsync("No channels are being redirected."),
            1 => context.RespondAsync($"<#{redirectedChannels[0]}> is being redirected."),
            2 => context.RespondAsync($"Both <#{redirectedChannels[0]}> and <#{redirectedChannels[1]}> are being redirected."),
            _ => context.RespondAsync(FormatChannelMentions(redirectedChannels))
        };
    }

    private static string FormatChannelMentions(IEnumerable<ulong> channelIds)
    {
        StringBuilder builder = new();
        builder.AppendLine("The following channels are being redirected:");
        foreach (ulong channelId in channelIds)
        {
            builder.AppendLine($"- <#{channelId}>");
        }
        return builder.ToString();
    }
}
