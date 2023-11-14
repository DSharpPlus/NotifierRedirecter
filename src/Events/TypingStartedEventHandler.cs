using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;

namespace NotifierRedirecter.Events;

public sealed class TypingStartedEventHandler(UserActivityTracker userActivityTracker)
{
    public Task ExecuteAsync(DiscordClient _, TypingStartEventArgs eventArgs)
    {
        userActivityTracker.UpdateUser(eventArgs.User.Id, eventArgs.Channel.Id);
        return Task.CompletedTask;
    }
}
