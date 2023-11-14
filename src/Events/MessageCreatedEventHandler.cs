using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace NotifierRedirecter.Events;

public sealed partial class MessageCreatedEventHandler(UserActivityTracker userActivityTracker, Database database, ILogger<MessageCreatedEventHandler>? logger = null)
{
    private readonly ILogger<MessageCreatedEventHandler> _logger = logger ?? NullLogger<MessageCreatedEventHandler>.Instance;

    public async Task ExecuteAsync(DiscordClient _, MessageCreateEventArgs eventArgs)
    {
        userActivityTracker.UpdateUser(eventArgs.Author.Id, eventArgs.Channel.Id);

        bool shouldSilence = eventArgs.Message.Flags?.HasFlag(MessageFlags.SupressNotifications) ?? false;
        DiscordMessage message = eventArgs.Message;

        // Explicitly cast to nullable to prevent erroneous compiler warning about it
        // not being nullable.
        DiscordMessage? reply = (DiscordMessage?)message.ReferencedMessage;

        // Ensure the channel is a redirect channel
        if (!database.IsRedirect(message.Channel.Id))
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
            // Check if the user has explicitly opted out of being pinged.
            // Additionally check if the user has recently done activity within the channel.
            if (user.IsBot || user == message.Author || await userActivityTracker.IsActiveAsync(user.Id, eventArgs.Channel.Id) || database.IsIgnoredUser(user.Id, eventArgs.Guild.Id, eventArgs.Channel.Id) || database.IsBlockedUser(user.Id, eventArgs.Guild.Id, eventArgs.Author.Id))
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
                    _logger.LogDebug(error, "User {UserId} doesn't exist!", user.Id);
                    continue;
                }
                catch (DiscordException error)
                {
                    _logger.LogError(error, "Failed to get member {UserId}", user.Id);
                    continue;
                }
                // This shouldn't hit but just in case I guess
                catch (Exception error)
                {
                    _logger.LogError(error, "Unexpected error when grabbing member {UserId}", user.Id);
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
                _logger.LogError(error, "Failed to send message to {UserId}", member.Id);
                continue;
            }
            catch (Exception error)
            {
                _logger.LogError(error, "Unexpected error when sending message to {UserId}", member.Id);
                continue;
            }
        }
    }
}
