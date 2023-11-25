using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Commands.Processors.TextCommands.Attributes;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Commands.Trees.Attributes;
using DSharpPlus.Entities;

namespace NotifierRedirecter.Commands;

public sealed class HelpCommand 
{
    [Command("help"), Description("Displays which commands are available, or displays detailed information for a specified command.")]
    [SuppressMessage("Roslyn", "IDE0046", Justification = "Ternary operator rabbit hole.")]
    public static ValueTask ExecuteAsync(CommandContext context, [Description("Which command to show information on. If empty, all commands are shown."), RemainingText] string? command = null)
    {
        IReadOnlyDictionary<string, Command> commands = context.Extension.Commands;
        if (string.IsNullOrWhiteSpace(command))
        {
            return context.RespondAsync(GenerateCommandListEmbed(context.User, commands.Values));
        }
        else if (!commands.TryGetValue(command, out Command? commandValue))
        {
            return context.RespondAsync($"Unable to find a command named {Formatter.InlineCode(command)}.");
        }
        else
        {
            return context.RespondAsync(GenerateCommandEmbed(context.User, commandValue));
        }
    }

    private static DiscordEmbedBuilder GenerateCommandListEmbed(DiscordUser author, IEnumerable<Command> commands)
    {
        DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
            .WithTitle("Commands")
            .WithDescription("Use `/help <command>` for more information on a command.")
            .WithColor(DiscordColor.Goldenrod)
            .WithAuthor(author.Username, author.AvatarUrl, author.AvatarUrl);

        foreach (Command command in commands)
        {
            // Remove subcommands from the list
            if (command.Parent is not null)
            {
                continue;
            }

            embed.AddField(command.Name, command.Description, true);
        }

        return embed;
    }

    private static DiscordEmbedBuilder GenerateCommandEmbed(DiscordUser author, Command command)
    {
        DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
            .WithTitle(command.FullName)
            .WithDescription(command.Description)
            .WithColor(DiscordColor.Goldenrod)
            .WithAuthor(author.Username, author.AvatarUrl, author.AvatarUrl);

        if (command.Subcommands.Any())
        {
            foreach (Command subcommand in command.Subcommands)
            {
                embed.AddField(subcommand.Name, subcommand.Description, true);
            }
        }
        else
        {
            StringBuilder usage = new($"Usage: `/{command.FullName.ToLowerInvariant()}");
            foreach (CommandParameter parameter in command.Overloads[0].Parameters)
            {
                embed.AddField(parameter.Name, parameter.Description, true);
                usage.Append(parameter.Flags.HasFlag(parameter.Attributes.Optional) ? $" [{parameter.Name}]" : $" <{parameter.Name}>");
            }
            usage.Append('`');
            embed.Description += $"\n{usage}";
        }

        return embed;
    }
}
