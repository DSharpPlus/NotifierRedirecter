namespace NotifierRedirecter.Configuration;

public sealed record DatabaseConfiguration
{
    public string Path { get; init; } = "database.db";
    public string? Password { get; init; }
}
