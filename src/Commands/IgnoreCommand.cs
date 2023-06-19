using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandAll.Attributes;
using DSharpPlus.CommandAll.Commands;
using DSharpPlus.CommandAll.Commands.Checks;
using DSharpPlus.Entities;

namespace NotifierRedirecter.Commands;

[Command("ignore"), Description("Determines the behavior of when you're pinged."), RequireGuildCheck]
public sealed class IgnoreCommand : BaseCommand
{
    [Command("add"), Description("Disable notifications from me.")]
    public static Task AddAsync(CommandContext context, [Description("Which channel to stop receiving notifications in. If empty, I'll stop sending notifications entirely")] DiscordChannel? channel = null)
    {
        if (Program.Database.IsIgnoredUser(context.User.Id, context.Guild!.Id, channel?.Id))
        {
            return context.ReplyAsync(channel is null
                ? "You are already on the global ignore list."
                : $"You're already ignoring {channel.Mention}."
            );
        }

        Program.Database.AddIgnoredUser(context.User.Id, context.Guild.Id, channel?.Id);
        return context.ReplyAsync(channel is null
            ? "Added you to the global ignore list. You will no longer receive notifications from me."
            : $"You will no longer be notified when you're pinged in {channel.Mention}."
        );
    }

    [Command("remove"), Description("Enable notifications from me.")]
    public static Task RemoveAsync(CommandContext context, [Description("Which channel to start receiving notifications in. If empty, I'll start sending notifications again")] DiscordChannel? channel = null)
    {
        if (!Program.Database.IsIgnoredUser(context.User.Id, context.Guild!.Id, channel?.Id))
        {
            return context.ReplyAsync(channel is null
                ? "You are not on the global ignore list."
                : $"You are not ignoring {channel.Mention}."
            );
        }

        Program.Database.RemoveIgnoredUser(context.User.Id, context.Guild.Id, channel?.Id);
        return context.ReplyAsync(channel is null
            ? "Removed you from the global ignore list. You will now receive notifications from me."
            : $"You will now be notified when you're pinged in {channel.Mention}."
        );
    }

    [Command("list"), Description("List all channels you've ignored.")]
    public static Task ListAsync(CommandContext context)
    {
        IReadOnlyList<ulong> ignoredChannels = Program.Database.ListIgnoredUserChannels(context.User.Id, context.Guild!.Id);
        return ignoredChannels.Count switch
        {
            0 => context.ReplyAsync("You are not ignoring any channels."),
            1 when ignoredChannels[0] == 0 => context.ReplyAsync("You are ignoring all channels."),
            1 => context.ReplyAsync($"You are ignoring {ignoredChannels[0]}."),
            2 => context.ReplyAsync($"You are ignoring {ignoredChannels[0]} and {ignoredChannels[1]}."),
            _ => context.ReplyAsync(FormatChannelMentions(ignoredChannels))
        };
    }

    private static string FormatChannelMentions(IEnumerable<ulong> channelIds)
    {
        StringBuilder builder = new();
        builder.AppendLine("You are ignoring the following channels:");
        foreach (ulong channelId in channelIds)
        {
            builder.AppendLine($"- <#{channelId}>");
        }
        return builder.ToString();
    }
}
