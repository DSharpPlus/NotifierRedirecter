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

namespace NotifierRedirecter.Events.Handlers;

public sealed partial class MessageCreatedEventHandler
{
    private readonly ILogger<MessageCreatedEventHandler> _logger;
    private readonly UserActivityTracker _userActivityTracker;
    private readonly Database _database;

    public MessageCreatedEventHandler(UserActivityTracker userActivityTracker, Database database, ILogger<MessageCreatedEventHandler>? logger = null)
    {
        this._userActivityTracker = userActivityTracker ?? throw new ArgumentNullException(nameof(userActivityTracker));
        this._database = database ?? throw new ArgumentNullException(nameof(database));
        this._logger = logger ?? NullLogger<MessageCreatedEventHandler>.Instance;
    }

    [DiscordEvent(DiscordIntents.GuildMessages | DiscordIntents.MessageContents)]
    public async Task ExecuteAsync(DiscordClient _, MessageCreateEventArgs eventArgs)
    {
        this._userActivityTracker.UpdateUser(eventArgs.Author.Id, eventArgs.Channel.Id);
        bool shouldSilence = eventArgs.Message.Flags?.HasFlag(MessageFlags.SuppressNotifications) ?? false;

        // Ensure the channel is a redirect channel
        if (eventArgs.Message.Channel is null || !this._database.IsRedirect(eventArgs.Message.Channel.Id))
        {
            return;
        }

        // Explicitly cast to nullable to prevent erroneous compiler
        // warning about it not being nullable.
        DiscordMessage? reply = eventArgs.Message.ReferencedMessage;
        IEnumerable<DiscordUser> mentionedUsers = eventArgs.Message.MentionedUsers;
        if (reply is not null && reply.MentionedUsers.Contains(reply.Author) && reply.Author! != eventArgs.Message.Author!)
        {
            mentionedUsers = mentionedUsers.Prepend(reply.Author!);
        }

        // Only mention the users that the message intended to mention.
        foreach (DiscordUser user in mentionedUsers)
        {
            // Check if the user has explicitly opted out of being pinged.
            // Additionally check if the user has recently done activity within the channel.
            if (user.IsBot
                || user == eventArgs.Message.Author!
                || await this._userActivityTracker.IsActiveAsync(user.Id, eventArgs.Channel.Id)
                || this._database.IsIgnoredUser(user.Id, eventArgs.Guild.Id, eventArgs.Channel.Id)
                || this._database.IsBlockedUser(user.Id, eventArgs.Guild.Id, eventArgs.Author.Id))
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
                    this._logger.LogDebug(error, "User {UserId} could not be found.", user.Id);
                    continue;
                }
                catch (DiscordException error)
                {
                    this._logger.LogError(error, "Failed to get member {UserId}", user.Id);
                    continue;
                }
                // This shouldn't hit but just in case I guess
                catch (Exception error)
                {
                    this._logger.LogError(error, "Unexpected error when grabbing member {UserId}", user.Id);
                    continue;
                }
            }

            try
            {
                DiscordMessageBuilder builder = new DiscordMessageBuilder()
                    .WithContent($"You were pinged by {eventArgs.Message.Author!.Mention} in {eventArgs.Channel.Mention}. [Jump! \u2197]({eventArgs.Message.JumpLink})");

                if (shouldSilence)
                {
                    builder.SuppressNotifications();
                }

                await member.SendMessageAsync(builder);
            }
            catch (DiscordException error)
            {
                this._logger.LogError(error, "Failed to send message to {UserId}", member.Id);
                continue;
            }
            catch (Exception error)
            {
                this._logger.LogError(error, "Unexpected error when sending message to {UserId}", member.Id);
                continue;
            }
        }
    }
}
