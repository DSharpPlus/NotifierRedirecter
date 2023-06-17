using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Logging;

namespace NotifierRedirecter
{
    internal static class Utils
    {
        public static async Task RedirectMention(DiscordClient client, MessageCreateEventArgs e, ulong userId)
        {
            DiscordMember? member = null;

            try
            {
                member = await e.Message.Channel.Guild.GetMemberAsync(userId);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                client.Logger.LogError("User {} doesn't exist!", member?.Username);
                return;
            }
            catch (Exception)
            {
                return; // Over engineering :D
            }

            await member.SendMessageAsync($"Notification: https://discord.com/channels/{e.Message.Channel.GuildId}/{e.Message.Channel.Id}/{e.Message.Id}");
        }
    }
}