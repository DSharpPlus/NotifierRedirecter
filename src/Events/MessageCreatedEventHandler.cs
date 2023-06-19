using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using Microsoft.Extensions.Logging;

namespace NotifierRedirecter.Events;

public sealed partial class MessageCreatedEventHandler
{
    private static readonly ILogger<MessageCreatedEventHandler> Logger = Program.LoggerFactory.CreateLogger<MessageCreatedEventHandler>();

    public static async Task ExecuteAsync(DiscordClient _, MessageCreateEventArgs eventArgs)
    {
        // Ensure the channel is a redirect channel
        if (!Program.Database.IsRedirect(eventArgs.Channel.Id))
        {
            return;
        }

        // Only mention the users that the message intended to mention.
        foreach (DiscordUser user in eventArgs.MentionedUsers)
        {
            // Check if the user has explicitly opted out of being pinged
            if (user.IsBot || Program.Database.IsIgnoredUser(user.Id, eventArgs.Guild.Id, eventArgs.Channel.Id))
            {
                continue;
            }

            // Attempt to see if the user is a DiscordMember (cached), otherwise attempt to grab the member from the API.
            if (user is not DiscordMember member)
            {
                try
                {
                    member = await eventArgs.Guild.GetMemberAsync(user.Id);
                }
                catch (NotFoundException error)
                {
                    Logger.LogDebug(error, "User {UserId} doesn't exist!", user.Id);
                    continue;
                }
                catch (DiscordException error)
                {
                    Logger.LogError(error, "Failed to get member {UserId}", user.Id);
                    continue;
                }
                // This shouldn't hit but just in case I guess
                catch (Exception error)
                {
                    Logger.LogError(error, "Unexpected error when grabbing member {UserId}", user.Id);
                    continue;
                }
            }

            try
            {
                await member.SendMessageAsync($"You were pinged in {eventArgs.Channel.Mention} by {eventArgs.Message.Author.Mention}: {eventArgs.Message.JumpLink}");
            }
            catch (DiscordException error)
            {
                Logger.LogError(error, "Failed to send message to {UserId}", member.Id);
                continue;
            }
            catch (Exception error)
            {
                Logger.LogError(error, "Unexpected error when sending message to {UserId}", member.Id);
                continue;
            }
        }
    }
}
