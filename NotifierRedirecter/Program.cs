using System.Text.Json;
using DSharpPlus;

namespace NotifierRedirecter;

public class Program
{
    public static Config JsonConfig { get; private set; } = null!;

    public static async Task Main()
    {
        Config? jsonConfig;
        await using (Stream file = File.OpenRead("./config.json"))
        {
            jsonConfig = await JsonSerializer.DeserializeAsync<Config>(file);
        }
        JsonConfig = jsonConfig ?? throw new Exception("Couldn't find config.json");

        DiscordConfiguration config = new()
        {
            Token = JsonConfig.Token,
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.Guilds | DiscordIntents.GuildMessages | DiscordIntents.MessageContents
        };

        DiscordClient client = new(config);

        client.MessageCreated += Events.MessageCreated;
        await client.ConnectAsync();
        await Task.Delay(-1);
    }

}