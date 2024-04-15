using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

        List<(string Formatting, string Message, bool IsUppercase)>? formattings = null;

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
                    .WithContent(GetDMContent(eventArgs, member, ref formattings));

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

    [GeneratedRegex(@"^(?<FORMAT>((#{1,3}\s+)|(>{1}\s+)|(>{3}\s+)|(\d+\.\s+))+)(?<MESSAGE>.*<\@!?(\d+?)>.*)", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ExplicitCapture)]
    private static partial Regex FormattingRegex();

    /// <summary>
    /// Gets the content of the direct message to send to the user.
    /// </summary>
    /// <param name="eventArgs">The message event arguements to base formatting off of.</param>
    /// <param name="member">The discord member being direct messaged.</param>
    /// <param name="formattings">Cached formattings.</param>
    /// <returns></returns>
    private static string GetDMContent(MessageCreateEventArgs eventArgs, DiscordMember member, ref List<(string Formatting, string Message, bool IsUppercase)>? formattings)
    {
        // Setup the formattings lookup if they haven't been cached yet.
        formattings ??= FormattingRegex()
                .Matches(eventArgs.Message.Content)
                .Select(match => (match.Groups[1].Value, match.Groups[2].Value, match.Groups[2].Value.Any(x => char.IsLetter(x) && !char.IsUpper(x)) == false))
                .ToList();

        string userMention = member.Mention;

        // Grab formatting of the message where the user is mentioned.
        (string Formatting, string Message, bool IsUppercase)? match = formattings.FirstOrDefault(x => x.Message.Contains(userMention));

        return match?.IsUppercase == true
            ? $"{match?.Formatting}YOU WERE PINGED BY {eventArgs.Message.Author!.Mention} IN {eventArgs.Channel.Mention}!! [JUMP!!! \u2197]({eventArgs.Message.JumpLink})"
            : $"{match?.Formatting}You were pinged by {eventArgs.Message.Author!.Mention} in {eventArgs.Channel.Mention}. [Jump! \u2197]({eventArgs.Message.JumpLink})";
    }
}
