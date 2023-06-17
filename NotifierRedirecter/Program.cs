using System.Text.Json;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace NotifierRedirecter;

public class Program
{
    public static Config JsonConfig { get; set; } = null!;

    public static async Task Main()
    {
        Config? jsonConfig;
        using (Stream file = File.OpenRead("./config.json"))
        {
            jsonConfig = await JsonSerializer.DeserializeAsync<Config>(file);
        }
        if (jsonConfig is null)
        {
            throw new Exception("Couldn't find config.json");
        }
        JsonConfig = jsonConfig;

        DiscordConfiguration config = new()
        {
            Token = JsonConfig.Token,
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.Guilds | DiscordIntents.GuildMessages | DiscordIntents.MessageContents
        };

        DiscordClient client = new(config);

        client.MessageCreated += MessageCreated;
        await client.ConnectAsync();
        await Task.Delay(-1);
    }

    public static Task MessageCreated(DiscordClient client, MessageCreateEventArgs e)
    {
        if (e.Channel.Id != JsonConfig.RedirectionChannel)
        {
            return Task.CompletedTask;
        }

        ReadOnlySpan<char> span = e.Message.Content.AsSpan();
        if (span.IsWhiteSpace())
        {
            return Task.CompletedTask;
        }

        HandleMentions(client, e, span, new());

        return Task.CompletedTask;
    }

    public static void HandleMentions(DiscordClient client, MessageCreateEventArgs e, ReadOnlySpan<char> span, HashSet<ulong> alreadyRedirected)
    {
        Console.WriteLine(span.ToString());

        int firstAppearence = span.IndexOf('<');
        if (firstAppearence == -1)
        {
            return;
        }
        if (firstAppearence != 0)
        {
            if (span[firstAppearence - 1] == '\\')
            {
                return;
            }
        }

        int lastAppearence = span.IndexOf('>');
        if (lastAppearence == -1)
        {
            return;
        }

        if (firstAppearence > lastAppearence)
        {
            goto label;
        }

        ReadOnlySpan<char> mention = span[firstAppearence..(lastAppearence + 1)];
        Console.WriteLine(mention.ToString());
        if (mention is not ['<', '@', .., '>'])
        {
            goto label;
        }

        ReadOnlySpan<char> userIdSpan = mention[2..^1];
        if (ulong.TryParse(userIdSpan, out ulong userId))
        {
            if (userId == e.Author.Id)
            {
                return;
            }

            if (alreadyRedirected.Contains(userId))
            {
                goto label;
            }
            alreadyRedirected.Add(userId);

            _ = RedirectMention(client, e, userId);
        }

    label:;
        if (!span.IsEmpty || span.IsWhiteSpace())
        {
            HandleMentions(client, e, span[lastAppearence..], alreadyRedirected);
        }
    }

    public static async Task RedirectMention(DiscordClient client, MessageCreateEventArgs e, ulong userId)
    {
        DiscordMember member;

        try
        {
            member = await e.Message.Channel.Guild.GetMemberAsync(userId);
        }
        catch (DSharpPlus.Exceptions.NotFoundException)
        {
            return;
        }
        catch (Exception)
        {
            return; // Over engineering :D
        }

        await member.SendMessageAsync($"Notification: https://discord.com/channels/{e.Message.Channel.GuildId}/{e.Message.Channel.Id}/{e.Message.Id}");
    }
}