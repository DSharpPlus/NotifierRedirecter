using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandAll.Attributes;
using DSharpPlus.CommandAll.Commands;
using DSharpPlus.CommandAll.Commands.Checks;
using DSharpPlus.Entities;

namespace NotifierRedirecter.Commands;

[Command("redirect"), Description("Manages which channels to start redirecting notifications for."), RequireGuildCheck]
public sealed class RedirectCommand : BaseCommand
{
    [Command("add"), Description("Start redirecting pings from this channel into user's DMs.")]
    [RequirePermissionsCheck(PermissionCheckType.User, Permissions.ManageMessages)]
    public static Task AddAsync(CommandContext context, [Description("Which channel to start listening for pings.")] DiscordChannel channel)
    {
        if (Program.Database.IsRedirect(channel.Id))
        {
            return context.ReplyAsync($"I'm already redirecting notifications from {channel.Mention}.");
        }

        Program.Database.AddRedirect(context.Guild!.Id, channel.Id);
        return context.ReplyAsync($"I've added {channel.Mention} to the redirect list.");
    }

    [Command("remove"), Description("Stop redirecting notifications from this channel into user's DMs.")]
    [RequirePermissionsCheck(PermissionCheckType.User, Permissions.ManageMessages)]
    public static Task RemoveAsync(CommandContext context, [Description("Which channel to stop listening for pings.")] DiscordChannel channel)
    {
        if (!Program.Database.IsRedirect(channel.Id))
        {
            return context.ReplyAsync($"I'm currently not redirecting notifications from {channel.Mention}.");
        }

        Program.Database.RemoveRedirect(context.Guild!.Id, channel.Id);
        return context.ReplyAsync($"I've removed {channel.Mention} from the redirect list. User's will no longer receive pings from that channel.");
    }

    [Command("list"), Description("List all channels I'm redirecting into user's DMs.")]
    public static Task ListAsync(CommandContext context)
    {
        IReadOnlyList<ulong> redirectedChannels = Program.Database.ListRedirects(context.Guild!.Id);
        return redirectedChannels.Count switch
        {
            0 => context.ReplyAsync("No channels are being redirected."),
            1 => context.ReplyAsync($"<#{redirectedChannels[0]}> is being redirected."),
            2 => context.ReplyAsync($"Both <#{redirectedChannels[0]}> and <#{redirectedChannels[1]}> are being redirected."),
            _ => context.ReplyAsync(FormatChannelMentions(redirectedChannels))
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
