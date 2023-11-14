using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandAll.Attributes;
using DSharpPlus.CommandAll.Commands;
using DSharpPlus.CommandAll.Commands.Checks;
using DSharpPlus.Entities;

namespace NotifierRedirecter.Commands;

[Command("ignore"), Description("Determines the behavior of when you're pinged."), RequireGuildCheck]
public sealed class IgnoreCommand(Database database) : BaseCommand
{
    [Command("add"), Description("Disable notifications from me.")]
    public Task AddAsync(CommandContext context, [Description("Which channel to stop receiving notifications in. If empty, I'll stop sending notifications entirely.")] DiscordChannel? channel = null)
    {
        if (database.IsIgnoredUser(context.User.Id, context.Guild!.Id, channel?.Id))
        {
            return context.ReplyAsync(channel is null
                ? "You're already on the global ignore list."
                : $"You're already ignoring {channel.Mention}."
            );
        }

        database.AddIgnoredUser(context.User.Id, context.Guild.Id, channel?.Id);
        return context.ReplyAsync(channel is null
            ? "I've added you to the global ignore list. You will no longer receive notifications from me."
            : $"You will no longer receive a DM when you're pinged in {channel.Mention}."
        );
    }

    [Command("remove"), Description("Enable notifications from me.")]
    public Task RemoveAsync(CommandContext context, [Description("Which channel to start receiving notifications in. If empty, I'll start sending notifications again.")] DiscordChannel? channel = null)
    {
        if (!database.IsIgnoredUser(context.User.Id, context.Guild!.Id, channel?.Id))
        {
            return context.ReplyAsync(channel is null
                ? "You're not on the global ignore list."
                : $"You're not ignoring {channel.Mention}."
            );
        }

        database.RemoveIgnoredUser(context.User.Id, context.Guild.Id, channel?.Id);
        return context.ReplyAsync(channel is null
            ? "I've removed you from the global ignore list. You will once again receive notifications from me."
            : $"You will now be notified when you're pinged in {channel.Mention}."
        );
    }

    [Command("list"), Description("List all channels you've ignored.")]
    public Task ListAsync(CommandContext context)
    {
        IReadOnlyList<ulong> ignoredChannels = database.ListIgnoredUserChannels(context.User.Id, context.Guild!.Id);
        return ignoredChannels.Count switch
        {
            0 => context.ReplyAsync("You're not ignoring any channels."),
            1 => context.ReplyAsync($"You're ignoring <#{ignoredChannels[0]}>."),
            2 => context.ReplyAsync($"You're ignoring <#{ignoredChannels[0]}> and <#{ignoredChannels[1]}>."),
            _ when ignoredChannels.Any(channelId => channelId == 0) => context.ReplyAsync("You're ignoring all channels."),
            _ => context.ReplyAsync(FormatChannelMentions(ignoredChannels))
        };
    }

    private static string FormatChannelMentions(IEnumerable<ulong> channelIds)
    {
        StringBuilder builder = new();
        builder.AppendLine("You're ignoring the following channels:");
        foreach (ulong channelId in channelIds)
        {
            builder.AppendLine($"- <#{channelId}>");
        }
        return builder.ToString();
    }
}
