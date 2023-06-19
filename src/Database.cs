using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data;
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
        _preparedCommands = PrepareCommands();
        PrepareDatabase();
    }

    public void AddRedirect(ulong guildId, ulong channelId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.AddRedirect];
        command.Parameters[0].Value = guildId;
        command.Parameters[1].Value = channelId;
        command.ExecuteNonQuery();
    }

    public bool IsRedirect(ulong channelId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.IsRedirect];
        command.Parameters[0].Value = channelId;
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.HasRows && reader.Read() && reader.GetBoolean(0);
    }

    public void RemoveRedirect(ulong guildId, ulong channelId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.RemoveRedirect];
        command.Parameters[0].Value = guildId;
        command.Parameters[1].Value = channelId;
        command.ExecuteNonQuery();
    }

    public void AddIgnoredUser(ulong userId, ulong guildId, ulong? channelId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.AddIgnoredUser];
        command.Parameters[0].Value = userId;
        command.Parameters[1].Value = guildId;
        command.Parameters[2].Value = (object?)channelId ?? DBNull.Value;
        command.ExecuteNonQuery();
    }

    public bool IsIgnoredUser(ulong userId, ulong guildId, ulong? channelId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.IsIgnoredUser];
        command.Parameters[0].Value = userId;
        command.Parameters[1].Value = guildId;
        command.Parameters[2].Value = (object?)channelId ?? DBNull.Value;
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.HasRows && reader.Read() && reader.GetBoolean(0);
    }

    public void RemoveIgnoredUser(ulong userId, ulong guildId, ulong? channelId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.RemoveIgnoredUser];
        command.Parameters[0].Value = userId;
        command.Parameters[1].Value = guildId;
        command.Parameters[2].Value = (object?)channelId ?? DBNull.Value;
        command.ExecuteNonQuery();
    }

    private FrozenDictionary<PreparedCommandType, SqliteCommand> PrepareCommands()
    {
        static SqliteParameter CreateParameter(SqliteCommand command, string name, bool nullable = false)
        {
            SqliteParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.IsNullable = nullable;
            parameter.SqliteType = SqliteType.Integer; // Can be a parameter, but hardcoding integer due to lack of a use case
            parameter.Size = 8; // All of our integers will be int-64
            return parameter;
        }

        SqliteCommand addRedirectCommand = _connection.CreateCommand();
        addRedirectCommand.CommandText = "INSERT INTO `redirects` (`guild_id`, `channel_id`) VALUES ($guild_id, $channel_id);";
        addRedirectCommand.Parameters.Add(CreateParameter(addRedirectCommand, "$guild_id"));
        addRedirectCommand.Parameters.Add(CreateParameter(addRedirectCommand, "$channel_id"));

        SqliteCommand isRedirectCommand = _connection.CreateCommand();
        isRedirectCommand.CommandText = "SELECT EXISTS(SELECT 1 FROM `redirects` WHERE `channel_id` = $channel_id LIMIT 1);";
        isRedirectCommand.Parameters.Add(CreateParameter(isRedirectCommand, "$channel_id"));

        SqliteCommand removeRedirectCommand = _connection.CreateCommand();
        removeRedirectCommand.CommandText = "DELETE FROM `redirects` WHERE `guild_id` = $guild_id AND `channel_id` = $channel_id;";
        removeRedirectCommand.Parameters.Add(CreateParameter(removeRedirectCommand, "$guild_id"));
        removeRedirectCommand.Parameters.Add(CreateParameter(removeRedirectCommand, "$channel_id"));

        SqliteCommand addIgnoredUserCommand = _connection.CreateCommand();
        addIgnoredUserCommand.CommandText = "INSERT INTO `ignored_users` (`user_id`, `guild_id`, `channel_id`) VALUES ($user_id, $guild_id, $channel_id);";
        addIgnoredUserCommand.Parameters.Add(CreateParameter(addIgnoredUserCommand, "$user_id"));
        addIgnoredUserCommand.Parameters.Add(CreateParameter(addIgnoredUserCommand, "$guild_id"));
        addIgnoredUserCommand.Parameters.Add(CreateParameter(addIgnoredUserCommand, "$channel_id", true));

        SqliteCommand isIgnoredUserCommand = _connection.CreateCommand();
        isIgnoredUserCommand.CommandText = "SELECT EXISTS(SELECT 1 FROM `ignored_users` WHERE `user_id` = $user_id AND `guild_id` = $guild_id AND (channel_id IS NULL OR `channel_id` = $channel_id) LIMIT 1);";
        isIgnoredUserCommand.Parameters.Add(CreateParameter(isIgnoredUserCommand, "$user_id"));
        isIgnoredUserCommand.Parameters.Add(CreateParameter(isIgnoredUserCommand, "$guild_id"));
        isIgnoredUserCommand.Parameters.Add(CreateParameter(isIgnoredUserCommand, "$channel_id", true));

        SqliteCommand removeIgnoredUserCommand = _connection.CreateCommand();
        removeIgnoredUserCommand.CommandText = "DELETE FROM `ignored_users` WHERE `user_id` = $user_id AND `guild_id` = $guild_id AND (channel_id IS NULL OR `channel_id` = $channel_id)";
        removeIgnoredUserCommand.Parameters.Add(CreateParameter(removeIgnoredUserCommand, "$user_id"));
        removeIgnoredUserCommand.Parameters.Add(CreateParameter(removeIgnoredUserCommand, "$guild_id"));
        removeIgnoredUserCommand.Parameters.Add(CreateParameter(removeIgnoredUserCommand, "$channel_id", true));

        return new Dictionary<PreparedCommandType, SqliteCommand>
        {
            [PreparedCommandType.AddRedirect] = addRedirectCommand,
            [PreparedCommandType.IsRedirect] = isRedirectCommand,
            [PreparedCommandType.RemoveRedirect] = removeRedirectCommand,
            [PreparedCommandType.AddIgnoredUser] = addIgnoredUserCommand,
            [PreparedCommandType.IsIgnoredUser] = isIgnoredUserCommand,
            [PreparedCommandType.RemoveIgnoredUser] = removeIgnoredUserCommand
        }.ToFrozenDictionary();
    }

    private void PrepareDatabase()
    {
        // https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async
        // > SQLite doesn't support asynchronous I/O. Async ADO.NET methods will execute synchronously in Microsoft.Data.Sqlite. Avoid calling them.
        // Prefer synchronous methods over asynchronous
        // > Instead, use write-ahead logging to improve performance and concurrency.
        // > WAL might be very slightly slower (perhaps 1% or 2% slower) than the traditional rollback-journal approach in applications that do mostly reads and seldom write.
        // Specifically not using "PRAGMA journal_mode = 'wal'" since we will be doing far more reads than writes.
        // Incoming messages are more common than interactions of course.
        _logger.LogTrace("Initializing SQL database...");
        _connection.Open();

        _logger.LogTrace("Creating SQL tables...");
        using SqliteCommand createTablesCommand = _connection.CreateCommand();
        createTablesCommand.CommandText = @"
            CREATE TABLE IF NOT EXISTS `redirects` (
                `guild_id` INTEGER NOT NULL,
                `channel_id` INTEGER NOT NULL,
                PRIMARY KEY (`guild_id`, `channel_id`)
            );

            CREATE TABLE IF NOT EXISTS `ignored_users` (
                `user_id` INTEGER NOT NULL,
                `guild_id` INTEGER NOT NULL,
                `channel_id` INTEGER,
                PRIMARY KEY (`user_id`, `guild_id`, `channel_id`)
            );
        ";
        createTablesCommand.ExecuteNonQuery();
        _logger.LogDebug("Created SQL tables.");

        for (int i = 0; i < _preparedCommands.Values.Length; i++)
        {
            SqliteCommand command = _preparedCommands.Values[i];
            _logger.LogTrace("Preparing command {CommandText}...", (PreparedCommandType)i);
            command.Prepare();
        }
        _logger.LogDebug("Prepared SQL commands.");
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
