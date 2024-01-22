using System;

namespace NotifierRedirecter.Events;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class DiscordEventAttribute : Attribute;
