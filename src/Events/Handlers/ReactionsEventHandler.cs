using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;

namespace NotifierRedirecter.Events.Handlers;

public sealed class ReactionsEventHandler
{
    private readonly UserActivityTracker _userActivityTracker;
    public ReactionsEventHandler(UserActivityTracker userActivityTracker) => this._userActivityTracker = userActivityTracker ?? throw new ArgumentNullException(nameof(userActivityTracker));

    [DiscordEvent(DiscordIntents.GuildMessageReactions)]
    public Task ExecuteAsync(DiscordClient _, MessageReactionAddEventArgs eventArgs)
    {
        this._userActivityTracker.UpdateUser(eventArgs.User.Id, eventArgs.Channel.Id);
        return Task.CompletedTask;
    }
}
