using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Commands.Trees.Attributes;
using DSharpPlus.Entities;

namespace NotifierRedirecter.Commands;

[Command("block"), Description("Blocks a specific user from pinging you."), RequireGuild]
public sealed class BlockCommand
{
    private readonly Database _database;
    public BlockCommand(Database database) => this._database = database ?? throw new ArgumentNullException(nameof(database));

    [Command("add"), Description("Blocks a user from pinging you.")]
    public ValueTask AddAsync(CommandContext context, [Description("Which user should no longer be allowed to ping you?")] DiscordUser user)
    {
        if (this._database.IsBlockedUser(context.User.Id, context.Guild!.Id, user.Id))
        {
            return context.RespondAsync($"You've already blocked {user.Mention}.");
        }

        this._database.AddBlockedUser(context.User.Id, context.Guild.Id, user.Id);
        return context.RespondAsync($"You will no longer receive a DM when you're pinged by {user.Mention}.");
    }

    [Command("remove"), Description("Unblock a user, allowing pings from them again.")]
    public ValueTask RemoveAsync(CommandContext context, [Description("Which user can ping you once more?")] DiscordUser user)
    {
        if (!this._database.IsBlockedUser(context.User.Id, context.Guild!.Id, user.Id))
        {
            return context.RespondAsync($"You don't have {user.Mention} blocked.");
        }

        this._database.RemoveBlockedUser(context.User.Id, context.Guild.Id, user.Id);
        return context.RespondAsync($"{user.Mention} can now ping you again.");
    }

    [Command("list"), Description("List all users you've blocked.")]
    public ValueTask ListAsync(CommandContext context)
    {
        IReadOnlyList<ulong> blockedUsers = this._database.ListBlockedUsers(context.User.Id, context.Guild!.Id);
        return blockedUsers.Count switch
        {
            0 => context.RespondAsync("You don't have any users blocked."),
            1 => context.RespondAsync($"You have blocked <@{blockedUsers[0]}>."),
            2 => context.RespondAsync($"You blocked <@{blockedUsers[0]}> and <@{blockedUsers[1]}>."),
            _ => context.RespondAsync(FormatUserMentions(blockedUsers))
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
