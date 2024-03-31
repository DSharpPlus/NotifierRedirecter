namespace NotifierRedirecter.Configuration;

public sealed class NotifierConfiguration
{
    public required DiscordConfiguration Discord { get; init; }
    public DatabaseConfiguration Database { get; init; } = new();
    public LoggerConfiguration Logger { get; init; } = new();
}
