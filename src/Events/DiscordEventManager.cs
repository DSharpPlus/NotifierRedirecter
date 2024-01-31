using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DSharpPlus;
using Microsoft.Extensions.DependencyInjection;

namespace NotifierRedirecter.Events;

public sealed class DiscordEventManager
{
    public DiscordIntents Intents { get; private set; }
    private readonly IServiceProvider _serviceProvider;
    private readonly List<MethodInfo> _eventHandlers = [];

    public DiscordEventManager(IServiceProvider serviceProvider) => this._serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public void GatherEventHandlers(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly, nameof(assembly));
        foreach (Type type in assembly.GetExportedTypes())
        {
            foreach (MethodInfo methodInfo in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (methodInfo.GetCustomAttribute<DiscordEventAttribute>() is DiscordEventAttribute eventAttribute)
                {
                    this.Intents |= eventAttribute.Intents;
                    this._eventHandlers.Add(methodInfo);
                }
            }
        }
    }

    public void RegisterEventHandlers(object obj)
    {
        ArgumentNullException.ThrowIfNull(obj, nameof(obj));
        foreach (EventInfo eventInfo in obj.GetType().GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            foreach (MethodInfo methodInfo in this._eventHandlers)
            {
                if (eventInfo.EventHandlerType!.GetGenericArguments().SequenceEqual(methodInfo.GetParameters().Select(parameter => parameter.ParameterType)))
                {
                    Delegate handler = methodInfo.IsStatic
                        ? Delegate.CreateDelegate(eventInfo.EventHandlerType, methodInfo)
                        : Delegate.CreateDelegate(eventInfo.EventHandlerType, ActivatorUtilities.CreateInstance(this._serviceProvider, methodInfo.DeclaringType!), methodInfo);

                    eventInfo.AddEventHandler(obj, handler);
                }
            }
        }
    }
}
