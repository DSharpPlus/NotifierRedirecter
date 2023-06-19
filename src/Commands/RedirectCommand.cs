using System.ComponentModel;
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
    [Command("add"), Description("Start redirecting notifications to this channel.")]
    [RequirePermissionsCheck(PermissionCheckType.User, Permissions.ManageMessages)]
    public static async Task AddAsync(CommandContext context, [Description("Which channel to start listening for pings.")] DiscordChannel channel)
    {
        if (await Program.Database.IsRedirectAsync(channel.Id))
        {
            await context.ReplyAsync($"Already redirecting notifications to {channel.Mention}.");
            return;
        }

        await Program.Database.AddRedirectAsync(channel.Id, context.Guild!.Id);
        await context.ReplyAsync($"Added {channel.Mention} to the redirect list.");
    }

    [Command("remove"), Description("Stop redirecting notifications to this channel.")]
    [RequirePermissionsCheck(PermissionCheckType.User, Permissions.ManageMessages)]
    public static async Task RemoveAsync(CommandContext context, [Description("Which channel to stop listening for pings.")] DiscordChannel channel)
    {
        if (!await Program.Database.IsRedirectAsync(channel.Id))
        {
            await context.ReplyAsync($"Not redirecting notifications to {channel.Mention}.");
            return;
        }

        await Program.Database.RemoveRedirectAsync(channel.Id, context.Guild!.Id);
        await context.ReplyAsync($"Removed {channel.Mention} from the redirect list.");
    }

    // TODO: ListAsync
}
