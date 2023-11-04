using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandAll.Attributes;
using DSharpPlus.CommandAll.Commands;
using DSharpPlus.CommandAll.Commands.Checks;
using DSharpPlus.Entities;

namespace NotifierRedirecter.Commands;

[Command("block"), Description("Blocks a specific user from pinging you."), RequireGuildCheck]
public sealed class BlockCommand : BaseCommand
{
    [Command("add"), Description("Blocks a user from pinging you.")]
    public static Task AddAsync(CommandContext context, [Description("Which user should no longer be allowed to ping you?")] DiscordUser user)
    {
        if (Program.Database.IsBlockedUser(context.User.Id, context.Guild!.Id, user.Id))
        {
            return context.ReplyAsync($"You've already blocked {user.Mention}."
            );
        }

        Program.Database.AddBlockedUser(context.User.Id, context.Guild.Id, user.Id);
        return context.ReplyAsync($"You will no longer receive a DM when you're pinged by {user.Mention}.");
    }

    [Command("remove"), Description("Unblock a user, allowing pings from them again.")]
    public static Task RemoveAsync(CommandContext context, [Description("Which user can ping you once more?")] DiscordUser user)
    {
        if (!Program.Database.IsBlockedUser(context.User.Id, context.Guild!.Id, user.Id))
        {
            return context.ReplyAsync($"You don't have {user.Mention} blocked.");
        }

        Program.Database.RemoveBlockedUser(context.User.Id, context.Guild.Id, user.Id);
        return context.ReplyAsync($"{user.Mention} can now ping you again.");
    }

    [Command("list"), Description("List all users you've blocked.")]
    public static Task ListAsync(CommandContext context)
    {
        IReadOnlyList<ulong> blockedUsers = Program.Database.ListBlockedUsers(context.User.Id, context.Guild!.Id);
        return blockedUsers.Count switch
        {
            0 => context.ReplyAsync("You don't have any users blocked."),
            1 => context.ReplyAsync($"You have blocked <@{blockedUsers[0]}>."),
            2 => context.ReplyAsync($"You blocked <@{blockedUsers[0]}> and <@{blockedUsers[1]}>."),
            _ => context.ReplyAsync(FormatUserMentions(blockedUsers))
        };
    }

    private static string FormatUserMentions(IEnumerable<ulong> userIds)
    {
        StringBuilder builder = new();
        builder.AppendLine("You've blocked the following users:");
        foreach (ulong userId in userIds)
        {
            builder.AppendLine($"- <@{userId}>");
        }
        return builder.ToString();
    }
}
