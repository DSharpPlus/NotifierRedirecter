using System;
using System.Collections.Generic;
using System.Linq;
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
        bool shouldSilence = eventArgs.Message.Flags?.HasFlag(MessageFlags.SupressNotifications) ?? false;
        DiscordMessage message = eventArgs.Message;

        // Explicitly cast to nullable to prevent erroneous compiler warning about it
        // not being nullable.
        DiscordMessage? reply = (DiscordMessage?)message.ReferencedMessage;

        // Ensure the channel is a redirect channel
        if (!Program.Database.IsRedirect(message.Channel.Id))
        {
            return;
        }

        IEnumerable<DiscordUser> mentionedUsers = message.MentionedUsers;
        if (reply is not null && reply.MentionedUsers.Contains(reply.Author))
        {
            mentionedUsers = mentionedUsers.Prepend(eventArgs.Message.ReferencedMessage.Author);
        }

        // Only mention the users that the message intended to mention.
        foreach (DiscordUser user in mentionedUsers)
        {
            // Check if the user has explicitly opted out of being pinged
            if (user.IsBot || user == message.Author || Program.Database.IsIgnoredUser(user.Id, eventArgs.Guild.Id, eventArgs.Channel.Id) || Program.Database.IsBlockedUser(user.Id, eventArgs.Guild.Id, eventArgs.Author.Id))
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
                DiscordMessageBuilder builder = new DiscordMessageBuilder()
                    .WithContent($"You were pinged by {message.Author.Mention} in {eventArgs.Channel.Mention}. [Jump! \u2197]({message.JumpLink})");

                if (shouldSilence)
                {
                    builder.SuppressNotifications();
                }

                await member.SendMessageAsync(builder);
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
