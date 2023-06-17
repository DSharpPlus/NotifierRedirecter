using System.ComponentModel;
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
    public static async Task AddAsync(CommandContext context, [Description("Which channel to stop receiving notifications in. If empty, I'll stop sending notifications entirely")] DiscordChannel? channel = null)
    {
        await Program.Database.AddIgnoredUserAsync(context.User.Id, channel?.Id);
        await context.ReplyAsync(channel is null
            ? "Added you to the global ignore list. You will no longer receive notifications from me."
            : $"You will no longer be notified when you're pinged in {channel.Mention}."
        );
    }

    [Command("remove"), Description("Enable notifications from me.")]
    public static async Task RemoveAsync(CommandContext context, [Description("Which channel to start receiving notifications in. If empty, I'll start sending notifications again")] DiscordChannel? channel = null)
    {
        if (!await Program.Database.IsIgnoredUserAsync(context.User.Id, channel?.Id))
        {
            await context.ReplyAsync(channel is null
                ? "You are not on the global ignore list."
                : $"You are not ignored in {channel.Mention}."
            );
            return;
        }

        await Program.Database.RemoveIgnoredUserAsync(context.User.Id, channel?.Id);
        await context.ReplyAsync(channel is null
            ? "Removed you from the global ignore list. You will now receive notifications from me."
            : $"You will now be notified when you're pinged in {channel.Mention}."
        );
    }

    // TODO: ListAsync
}
