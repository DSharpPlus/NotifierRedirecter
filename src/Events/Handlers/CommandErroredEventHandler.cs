using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandAll;
using DSharpPlus.CommandAll.Commands.Checks;
using DSharpPlus.CommandAll.EventArgs;
using DSharpPlus.CommandAll.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Humanizer;

namespace NotifierRedirecter.Events.Handlers;

public sealed class CommandErroredEventHandler
{
    public static Task ExecuteAsync(CommandAllExtension _, CommandErroredEventArgs eventArgs)
    {
        if (eventArgs.Exception is CommandNotFoundException commandNotFoundException)
        {
            return eventArgs.Context.ReplyAsync($"Unknown command: {commandNotFoundException.CommandString}");
        }

        DiscordEmbedBuilder embedBuilder = new()
        {
            Title = "Command Error",
            Description = $"{Formatter.InlineCode(eventArgs.Context.CurrentCommand.FullName)} failed to execute.",
            Color = new DiscordColor("#6b73db")
        };

        switch (eventArgs.Exception)
        {
            case CommandChecksFailedException checksFailedException:
                embedBuilder.AddField("Error Message", checksFailedException.Message, true);
                foreach (CommandCheckResult check in checksFailedException.FailedChecks)
                {
                    if (check.Success)
                    {
                        continue;
                    }

                    embedBuilder = check.Check switch
                    {
                        RequireGuildCheckAttribute => embedBuilder.AddField("Guild Only", "This command can only be used in a guild.", false),
                        RequirePermissionsCheckAttribute permissionsCheck when permissionsCheck.PermissionType == PermissionCheckType.Bot => embedBuilder.AddField("I'm Missing Permissions", string.Join(", ", permissionsCheck.Permissions.ToPermissionString()), false),
                        RequirePermissionsCheckAttribute permissionsCheck when permissionsCheck.PermissionType == PermissionCheckType.User => embedBuilder.AddField("You're Missing Permissions", string.Join(", ", permissionsCheck.Permissions.ToPermissionString()), false),
                        _ => embedBuilder.AddField(check.Check.GetType().Name, check.Exception?.Message ?? "Failed.", false)
                    };
                }
                break;
            case DiscordException discordError:
                embedBuilder.AddField("HTTP Code", discordError.WebResponse.ResponseCode.ToString(), true);
                embedBuilder.AddField("Error Message", discordError.JsonMessage, true);
                break;
            default:
                embedBuilder.AddField("Error Message", eventArgs.Exception.Message, true);
                embedBuilder.AddField("Stack Trace", Formatter.BlockCode(FormatStackTrace(eventArgs.Exception.StackTrace).Truncate(1014, "…"), "cs"), false);
                break;
        }

        return eventArgs.Context.ReplyAsync(new DiscordMessageBuilder().AddEmbed(embedBuilder));
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
