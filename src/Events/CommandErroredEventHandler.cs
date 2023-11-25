using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.EventArgs;
using DSharpPlus.Commands.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;

namespace NotifierRedirecter.Events;

public sealed class CommandErroredEventHandler
{
    public static ValueTask ExecuteAsync(CommandsExtension _, CommandErroredEventArgs eventArgs)
    {
        if (eventArgs.Exception is CommandNotFoundException commandNotFoundException)
        {
            return eventArgs.Context.RespondAsync($"Unknown command: {commandNotFoundException.CommandName}");
        }

        DiscordEmbedBuilder embedBuilder = new()
        {
            Title = "Command Error",
            Description = $"{Formatter.InlineCode(eventArgs.Context.Command.FullName)} failed to execute.",
            Color = new DiscordColor("#6b73db")
        };

        switch (eventArgs.Exception)
        {
            case ChecksFailedException checksFailedException:
                embedBuilder.AddField("Error Message", checksFailedException.Message, true);
                foreach (var check in checksFailedException.Check)
                {
                    embedBuilder = check.Check switch
                    {
                        RequireGuildAttribute => embedBuilder.AddField("Guild Only", "This command can only be used in a guild.", false),
                        RequirePermissionsAttribute permissionsCheck when permissionsCheck.BotPermissions != Permissions.None => embedBuilder.AddField("I'm Missing Permissions", string.Join(", ", permissionsCheck.BotPermissions.ToPermissionString()), false),
                        RequirePermissionsAttribute permissionsCheck when permissionsCheck.UserPermissions == Permissions.None => embedBuilder.AddField("You're Missing Permissions", string.Join(", ", permissionsCheck.UserPermissions.ToPermissionString()), false),
                        _ => embedBuilder.AddField(check.Message.GetType().Name, check.InnerException?.Message ?? "Failed.", false)
                    };
                }
                break;
            case DiscordException discordError:
                embedBuilder.AddField("HTTP Code", discordError.Response?.ToString(), true);
                embedBuilder.AddField("Error Message", discordError.JsonMessage, true);
                break;
            default:
                embedBuilder.AddField("Error Message", eventArgs.Exception.Message, true);
                embedBuilder.AddField("Stack Trace", Formatter.BlockCode(FormatStackTrace(eventArgs.Exception.StackTrace).Truncate(1014, "…"), "cs"), false);
                break;
        }

        return eventArgs.Context.RespondAsync(new DiscordMessageBuilder().AddEmbed(embedBuilder));
    }

    private static string FormatStackTrace(string? text) => text == null
        ? "No stack trace available."
        : string.Join('\n', text.Split('\n').Select(line => ReplaceFirst(line.Trim(), "at", "-")));

    private static string ReplaceFirst(string text, string search, string replace)
    {
        ReadOnlySpan<char> textSpan = text.AsSpan();
        int pos = textSpan.IndexOf(search);
        return pos < 0 ? text : string.Concat(textSpan[..pos], replace, textSpan[(pos + search.Length)..]);
    }
}
