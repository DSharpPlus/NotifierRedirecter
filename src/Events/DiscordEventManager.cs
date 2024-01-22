using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace NotifierRedirecter.Events;

public sealed class DiscordEventManager
{
    private readonly IServiceProvider ServiceProvider;
    private readonly List<MethodInfo> EventHandlers = [];

    public DiscordEventManager(IServiceProvider serviceProvider) => this.ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public void GatherEventHandlers(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        foreach (Type type in assembly.GetExportedTypes())
        {
            foreach (MethodInfo methodInfo in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (methodInfo.GetCustomAttribute<DiscordEventAttribute>() is not null)
                {
                    this.EventHandlers.Add(methodInfo);
                }
            }
        }
    }

    public void RegisterEventHandlers(object obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        foreach (EventInfo eventInfo in obj.GetType().GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            foreach (MethodInfo methodInfo in this.EventHandlers)
            {
                if (eventInfo.EventHandlerType!.GetGenericArguments().SequenceEqual(methodInfo.GetParameters().Select(parameter => parameter.ParameterType)))
                {
                    Delegate handler = methodInfo.IsStatic
                        ? Delegate.CreateDelegate(eventInfo.EventHandlerType, methodInfo)
                        : Delegate.CreateDelegate(eventInfo.EventHandlerType, ActivatorUtilities.CreateInstance(this.ServiceProvider, methodInfo.DeclaringType!), methodInfo);

                    eventInfo.AddEventHandler(obj, handler);
                }
            }
        }
    }
}
