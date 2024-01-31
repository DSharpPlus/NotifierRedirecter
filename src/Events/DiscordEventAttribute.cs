using System;
using DSharpPlus;

namespace NotifierRedirecter.Events;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class DiscordEventAttribute : Attribute
{
    public DiscordIntents Intents { get; init; }
    public DiscordEventAttribute(DiscordIntents intents = 0) => this.Intents = intents;
}
