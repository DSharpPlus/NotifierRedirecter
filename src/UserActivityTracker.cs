using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NotifierRedirecter;

public sealed class UserActivityTracker
{
    private static readonly TimeSpan _activityTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Tracks the user's last activity within a channel.
    /// </summary>
    private readonly ConcurrentDictionary<ulong, (ulong ChannelId, DateTimeOffset LastActivity)> _tracker = new();

    public UserActivityTracker() => _ = CleanupInactiveUsersAsync();

    public void UpdateUser(ulong userId, ulong channelId) => this._tracker.AddOrUpdate(userId, (channelId, DateTimeOffset.UtcNow), (_, _) => (channelId, DateTimeOffset.UtcNow));
    public async ValueTask<bool> IsActiveAsync(ulong userId, ulong channelId)
    {
        // Wait 5 seconds to see if the user does anything in the channnel.
        // If they do, the _tracker will be updated and we can avoid a DM.
        await Task.Delay(_activityTimeout);
        return this._tracker.TryGetValue(userId, out (ulong ChannelId, DateTimeOffset LastActivity) value) && value.ChannelId == channelId && value.LastActivity > DateTimeOffset.UtcNow.AddSeconds(-15);
    }

    public async Task CleanupInactiveUsersAsync()
    {
        PeriodicTimer timer = new(_cleanupInterval);
        while (await timer.WaitForNextTickAsync())
        {
            foreach ((ulong userId, (ulong _, DateTimeOffset LastActivity)) in this._tracker)
            {
                // If the user hasn't done anything since the last cleanup interval, remove them from the tracker.
                if (LastActivity < DateTimeOffset.UtcNow.Add(-_cleanupInterval))
                {
                    this._tracker.TryRemove(userId, out _);
                }
            }
        }
    }
}
