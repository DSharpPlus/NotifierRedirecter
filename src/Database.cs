using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NotifierRedirecter;

public sealed class Database
{
    // Sqlite is thread safe by default, meaning it's safe to use the same connection across multiple threads: https://stackoverflow.com/a/39916593/10942966
    private readonly SqliteConnection _connection;
    private readonly ILogger<Database> _logger;
    private readonly FrozenDictionary<PreparedCommandType, SqliteCommand> _preparedCommands;

    public Database(IConfiguration configuration)
    {
        _logger = Program.LoggerFactory.CreateLogger<Database>();
        string? dataSource = configuration.GetValue("database:path", "database.db");
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            _logger.LogCritical("Database path is not set.");
            Environment.Exit(1);
            return; // For the compiler and nullability
        }

        _connection = new SqliteConnection(new SqliteConnectionStringBuilder()
        {
            Cache = SqliteCacheMode.Private,
            DataSource = dataSource,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Password = configuration.GetValue<string?>("database:password")
        }.ToString());

        SqliteCommand addRedirectCommand = _connection.CreateCommand();
        addRedirectCommand.CommandText = "INSERT INTO `redirects` (`guild_id`, `channel_id`) VALUES ($guild_id, $channel_id);";
        addRedirectCommand.Parameters.Add("$guild_id");
        addRedirectCommand.Parameters.Add("$channel_id");

        SqliteCommand isRedirectCommand = _connection.CreateCommand();
        isRedirectCommand.CommandText = "SELECT EXISTS(SELECT 1 FROM `redirects` WHERE `channel_id` = $channel_id LIMIT 1);";
        isRedirectCommand.Parameters.Add("$channel_id");

        SqliteCommand removeRedirectCommand = _connection.CreateCommand();
        removeRedirectCommand.CommandText = "DELETE FROM `redirects` WHERE `guild_id` = $guild_id AND `channel_id` = $channel_id;";
        removeRedirectCommand.Parameters.Add("$guild_id");
        removeRedirectCommand.Parameters.Add("$channel_id");

        SqliteCommand addIgnoredUserCommand = _connection.CreateCommand();
        addIgnoredUserCommand.CommandText = "INSERT INTO `ignored_users` (`user_id`, `channel_id`) VALUES ($guild_id, $user_id, $channel_id);";
        addIgnoredUserCommand.Parameters.Add("$user_id");
        addIgnoredUserCommand.Parameters.Add("$channel_id");

        SqliteCommand isIgnoredUserCommand = _connection.CreateCommand();
        isIgnoredUserCommand.CommandText = "SELECT EXISTS(SELECT 1 FROM `ignored_users` WHERE `user_id` = $user_id AND `channel_id` = $channel_id LIMIT 1);";
        isIgnoredUserCommand.Parameters.Add("$user_id");
        isIgnoredUserCommand.Parameters.Add("$channel_id");

        SqliteCommand removeIgnoredUserCommand = _connection.CreateCommand();
        removeIgnoredUserCommand.CommandText = "DELETE FROM `ignored_users` WHERE `user_id` = $user_id AND `channel_id` = $channel_id;";
        removeIgnoredUserCommand.Parameters.Add("$user_id");
        removeIgnoredUserCommand.Parameters.Add("$channel_id");

        _preparedCommands = new Dictionary<PreparedCommandType, SqliteCommand>
        {
            [PreparedCommandType.AddRedirect] = addRedirectCommand,
            [PreparedCommandType.IsRedirect] = isRedirectCommand,
            [PreparedCommandType.RemoveRedirect] = removeRedirectCommand,
            [PreparedCommandType.AddIgnoredUser] = addIgnoredUserCommand,
            [PreparedCommandType.IsIgnoredUser] = isIgnoredUserCommand,
            [PreparedCommandType.RemoveIgnoredUser] = removeIgnoredUserCommand
        }.ToFrozenDictionary();
    }

    public async Task InitializeAsync()
    {
        _logger.LogTrace("Initializing SQL database...");
        await _connection.OpenAsync();

        _logger.LogTrace("Creating SQL tables...");
        await using SqliteCommand createTablesCommand = _connection.CreateCommand();
        createTablesCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS `redirects` (
                `guild_id` INTEGER NOT NULL,
                `channel_id` INTEGER NOT NULL,
                PRIMARY KEY (`guild_id`, `channel_id`)
            );

            CREATE TABLE IF NOT EXISTS `ignored_users` (
                `user_id` INTEGER NOT NULL,
                `channel_id` INTEGER,
                PRIMARY KEY (`user_id`, `channel_id`)
            );
        ";
        await createTablesCommand.ExecuteNonQueryAsync();
        _logger.LogDebug("Created SQL tables.");

        for (int i = 0; i < _preparedCommands.Values.Length; i++)
        {
            SqliteCommand command = _preparedCommands.Values[i];
            _logger.LogTrace("Preparing command {CommandText}...", (PreparedCommandType)i);
            await command.PrepareAsync();
        }
        _logger.LogDebug("Prepared SQL commands.");
    }

    public Task AddRedirectAsync(ulong guildId, ulong channelId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.AddRedirect];
        command.Parameters[0].Value = guildId;
        command.Parameters[1].Value = channelId;
        return command.ExecuteNonQueryAsync();
    }

    public Task<bool> IsRedirectAsync(ulong channelId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.IsRedirect];
        command.Parameters[0].Value = channelId;
        return command.ExecuteScalarAsync().ContinueWith(task => task.Result is not null);
    }

    public Task RemoveRedirectAsync(ulong guildId, ulong channelId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.RemoveRedirect];
        command.Parameters[0].Value = guildId;
        command.Parameters[1].Value = channelId;
        return command.ExecuteNonQueryAsync();
    }

    public Task AddIgnoredUserAsync(ulong userId, ulong? channelId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.AddIgnoredUser];
        command.Parameters[0].Value = userId;
        command.Parameters[1].Value = channelId;
        return command.ExecuteNonQueryAsync();
    }

    public Task<bool> IsIgnoredUserAsync(ulong userId, ulong? channelId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.IsIgnoredUser];
        command.Parameters[0].Value = userId;
        command.Parameters[1].Value = channelId;
        return command.ExecuteScalarAsync().ContinueWith(task => task.Result is not null);
    }

    public Task RemoveIgnoredUserAsync(ulong userId, ulong? channelId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.RemoveIgnoredUser];
        command.Parameters[0].Value = userId;
        command.Parameters[1].Value = channelId;
        return command.ExecuteNonQueryAsync();
    }

    private enum PreparedCommandType
    {
        AddRedirect,
        IsRedirect,
        RemoveRedirect,
        AddIgnoredUser,
        IsIgnoredUser,
        RemoveIgnoredUser,
    }
}
