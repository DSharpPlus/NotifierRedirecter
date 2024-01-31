using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;

namespace NotifierRedirecter.Events.Handlers;

public sealed class TypingStartedEventHandler
{
    private readonly UserActivityTracker _userActivityTracker;
    public TypingStartedEventHandler(UserActivityTracker userActivityTracker) => this._userActivityTracker = userActivityTracker ?? throw new ArgumentNullException(nameof(userActivityTracker));

    [DiscordEvent(DiscordIntents.GuildMessageTyping)]
    public Task ExecuteAsync(DiscordClient _, TypingStartEventArgs eventArgs)
    {
        this._userActivityTracker.UpdateUser(eventArgs.User.Id, eventArgs.Channel.Id);
        return Task.CompletedTask;
    }
}
