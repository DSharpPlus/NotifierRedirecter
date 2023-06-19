using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandAll;
using DSharpPlus.CommandAll.EventArgs;
using DSharpPlus.CommandAll.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Humanizer;
using System.Linq;
using System;

namespace NotifierRedirecter.Events;

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

                    embedBuilder.AddField(check.Check.GetType().Name, check.Exception?.Message ?? "Failed.", false);
                }
                return eventArgs.Context.ReplyAsync(new DiscordMessageBuilder().AddEmbed(embedBuilder));
            case DiscordException discordError:
                embedBuilder.AddField("HTTP Code", discordError.WebResponse.ResponseCode.ToString(), true);
                embedBuilder.AddField("Error Message", discordError.JsonMessage, true);
                return eventArgs.Context.ReplyAsync(new DiscordMessageBuilder().AddEmbed(embedBuilder));
            default:
                embedBuilder.AddField("Error Message", eventArgs.Exception.Message, true);
                embedBuilder.AddField("Stack Trace", Formatter.BlockCode(FormatStackTrace(eventArgs.Exception.StackTrace).Truncate(1014, "…"), "cs"), false);
                return eventArgs.Context.ReplyAsync(new DiscordMessageBuilder().AddEmbed(embedBuilder));
        }
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