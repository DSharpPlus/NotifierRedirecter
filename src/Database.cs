using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace NotifierRedirecter;

public sealed class Database
{
    // Sqlite is thread safe by default, meaning it's safe to use the same connection across multiple threads: https://stackoverflow.com/a/39916593/10942966
    private readonly SqliteConnection _connection;
    private readonly ILogger<Database> _logger;
    private readonly FrozenDictionary<PreparedCommandType, SqliteCommand> _preparedCommands;

    public Database(IConfiguration configuration, ILogger<Database>? logger = null)
    {
        _logger = logger ?? NullLogger<Database>.Instance;
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

    public IReadOnlyList<ulong> ListRedirects(ulong guildId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.ListRedirects];
        command.Parameters[0].Value = guildId;
        using SqliteDataReader reader = command.ExecuteReader();
        List<ulong> channels = [];
        while (reader.Read())
        {
            channels.Add(reader.GetFieldValue<ulong>(0));
        }

        return channels;
    }

    public void AddIgnoredUser(ulong userId, ulong guildId, ulong? channelId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.AddIgnoredUser];
        command.Parameters[0].Value = userId;
        command.Parameters[1].Value = guildId;
        command.Parameters[2].Value = channelId ?? 0;
        command.ExecuteNonQuery();
    }

    public bool IsIgnoredUser(ulong userId, ulong guildId, ulong? channelId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.IsIgnoredUser];
        command.Parameters[0].Value = userId;
        command.Parameters[1].Value = guildId;
        command.Parameters[2].Value = channelId ?? 0;
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.HasRows && reader.Read() && reader.GetBoolean(0);
    }

    public void RemoveIgnoredUser(ulong userId, ulong guildId, ulong? channelId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.RemoveIgnoredUser];
        command.Parameters[0].Value = userId;
        command.Parameters[1].Value = guildId;
        command.Parameters[2].Value = channelId ?? 0;
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<ulong> ListIgnoredUserChannels(ulong userId, ulong guildId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.ListIgnoredUserChannels];
        command.Parameters[0].Value = userId;
        command.Parameters[1].Value = guildId;
        using SqliteDataReader reader = command.ExecuteReader();
        List<ulong> channels = [];
        while (reader.Read())
        {
            channels.Add(reader.GetFieldValue<ulong>(0));
        }

        return channels;
    }

    public void AddBlockedUser(ulong userId, ulong guildId, ulong blockedUserId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.AddBlockedUser];
        command.Parameters[0].Value = userId;
        command.Parameters[1].Value = guildId;
        command.Parameters[2].Value = blockedUserId;
        command.ExecuteNonQuery();
    }

    public bool IsBlockedUser(ulong userId, ulong guildId, ulong blockedUserId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.IsBlockedUser];
        command.Parameters[0].Value = userId;
        command.Parameters[1].Value = guildId;
        command.Parameters[2].Value = blockedUserId;
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.HasRows && reader.Read() && reader.GetBoolean(0);
    }

    public void RemoveBlockedUser(ulong userId, ulong guildId, ulong blockedUserId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.RemoveBlockedUser];
        command.Parameters[0].Value = userId;
        command.Parameters[1].Value = guildId;
        command.Parameters[2].Value = blockedUserId;
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<ulong> ListBlockedUsers(ulong userId, ulong guildId)
    {
        SqliteCommand command = _preparedCommands[PreparedCommandType.ListBlockedUsers];
        command.Parameters[0].Value = userId;
        command.Parameters[1].Value = guildId;
        using SqliteDataReader reader = command.ExecuteReader();
        List<ulong> blockedUsers = [];
        while (reader.Read())
        {
            blockedUsers.Add(reader.GetFieldValue<ulong>(0));
        }

        return blockedUsers;
    }

    private FrozenDictionary<PreparedCommandType, SqliteCommand> PrepareCommands()
    {
        static SqliteParameter CreateParameter(SqliteCommand command, string name)
        {
            SqliteParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
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

        SqliteCommand listRedirectsCommand = _connection.CreateCommand();
        listRedirectsCommand.CommandText = "SELECT `channel_id` FROM `redirects` WHERE `guild_id` = $guild_id;";
        listRedirectsCommand.Parameters.Add(CreateParameter(listRedirectsCommand, "$guild_id"));

        SqliteCommand addIgnoredUserCommand = _connection.CreateCommand();
        addIgnoredUserCommand.CommandText = "INSERT INTO `ignored_users` (`user_id`, `guild_id`, `channel_id`) VALUES ($user_id, $guild_id, $channel_id);";
        addIgnoredUserCommand.Parameters.Add(CreateParameter(addIgnoredUserCommand, "$user_id"));
        addIgnoredUserCommand.Parameters.Add(CreateParameter(addIgnoredUserCommand, "$guild_id"));
        addIgnoredUserCommand.Parameters.Add(CreateParameter(addIgnoredUserCommand, "$channel_id"));

        SqliteCommand isIgnoredUserCommand = _connection.CreateCommand();
        isIgnoredUserCommand.CommandText = "SELECT EXISTS(SELECT 1 FROM `ignored_users` WHERE `user_id` = $user_id AND `guild_id` = $guild_id AND (channel_id = 0 OR `channel_id` = $channel_id) LIMIT 1);";
        isIgnoredUserCommand.Parameters.Add(CreateParameter(isIgnoredUserCommand, "$user_id"));
        isIgnoredUserCommand.Parameters.Add(CreateParameter(isIgnoredUserCommand, "$guild_id"));
        isIgnoredUserCommand.Parameters.Add(CreateParameter(isIgnoredUserCommand, "$channel_id"));

        SqliteCommand removeIgnoredUserCommand = _connection.CreateCommand();
        removeIgnoredUserCommand.CommandText = "DELETE FROM `ignored_users` WHERE `user_id` = $user_id AND `guild_id` = $guild_id AND (channel_id = 0 OR `channel_id` = $channel_id)";
        removeIgnoredUserCommand.Parameters.Add(CreateParameter(removeIgnoredUserCommand, "$user_id"));
        removeIgnoredUserCommand.Parameters.Add(CreateParameter(removeIgnoredUserCommand, "$guild_id"));
        removeIgnoredUserCommand.Parameters.Add(CreateParameter(removeIgnoredUserCommand, "$channel_id"));

        SqliteCommand listIgnoredUserChannels = _connection.CreateCommand();
        listIgnoredUserChannels.CommandText = "SELECT `channel_id` FROM `ignored_users` WHERE `user_id` = $user_id AND `guild_id` = $guild_id;";
        listIgnoredUserChannels.Parameters.Add(CreateParameter(listIgnoredUserChannels, "$user_id"));
        listIgnoredUserChannels.Parameters.Add(CreateParameter(listIgnoredUserChannels, "$guild_id"));

        SqliteCommand addBlockedUserCommand = _connection.CreateCommand();
        addBlockedUserCommand.CommandText = "INSERT INTO `blocked_users` (`user_id`, `guild_id`, `blocked_user_id`) VALUES ($user_id, $guild_id, $blocked_user_id);";
        addBlockedUserCommand.Parameters.Add(CreateParameter(addBlockedUserCommand, "$user_id"));
        addBlockedUserCommand.Parameters.Add(CreateParameter(addBlockedUserCommand, "$guild_id"));
        addBlockedUserCommand.Parameters.Add(CreateParameter(addBlockedUserCommand, "$blocked_user_id"));

        SqliteCommand isBlockedUserCommand = _connection.CreateCommand();
        isBlockedUserCommand.CommandText = "SELECT EXISTS(SELECT 1 FROM `blocked_users` WHERE `user_id` = $user_id AND `guild_id` = $guild_id AND `blocked_user_id` = $blocked_user_id LIMIT 1);";
        isBlockedUserCommand.Parameters.Add(CreateParameter(isBlockedUserCommand, "$user_id"));
        isBlockedUserCommand.Parameters.Add(CreateParameter(isBlockedUserCommand, "$guild_id"));
        isBlockedUserCommand.Parameters.Add(CreateParameter(isBlockedUserCommand, "$blocked_user_id"));

        SqliteCommand removeBlockedUserCommand = _connection.CreateCommand();
        removeBlockedUserCommand.CommandText = "DELETE FROM `blocked_users` WHERE `user_id` = $user_id AND `guild_id` = $guild_id AND `blocked_user_id` = $blocked_user_id;";
        removeBlockedUserCommand.Parameters.Add(CreateParameter(removeBlockedUserCommand, "$user_id"));
        removeBlockedUserCommand.Parameters.Add(CreateParameter(removeBlockedUserCommand, "$guild_id"));
        removeBlockedUserCommand.Parameters.Add(CreateParameter(removeBlockedUserCommand, "$blocked_user_id"));

        SqliteCommand listBlockedUsers = _connection.CreateCommand();
        listBlockedUsers.CommandText = "SELECT `blocked_user_id` FROM `blocked_users` WHERE `user_id` = $user_id AND `guild_id` = $guild_id;";
        listBlockedUsers.Parameters.Add(CreateParameter(listBlockedUsers, "$user_id"));
        listBlockedUsers.Parameters.Add(CreateParameter(listBlockedUsers, "$guild_id"));

        return new Dictionary<PreparedCommandType, SqliteCommand>
        {
            [PreparedCommandType.AddRedirect] = addRedirectCommand,
            [PreparedCommandType.IsRedirect] = isRedirectCommand,
            [PreparedCommandType.RemoveRedirect] = removeRedirectCommand,
            [PreparedCommandType.ListRedirects] = listRedirectsCommand,
            [PreparedCommandType.AddIgnoredUser] = addIgnoredUserCommand,
            [PreparedCommandType.IsIgnoredUser] = isIgnoredUserCommand,
            [PreparedCommandType.RemoveIgnoredUser] = removeIgnoredUserCommand,
            [PreparedCommandType.ListIgnoredUserChannels] = listIgnoredUserChannels,
            [PreparedCommandType.AddBlockedUser] = addBlockedUserCommand,
            [PreparedCommandType.IsBlockedUser] = isBlockedUserCommand,
            [PreparedCommandType.RemoveBlockedUser] = removeBlockedUserCommand,
            [PreparedCommandType.ListBlockedUsers] = listBlockedUsers
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
        // Incoming messages are more common than interactions.
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
                `channel_id` INTEGER NOT NULL,
                PRIMARY KEY (`user_id`, `guild_id`, `channel_id`)
            );

            CREATE TABLE IF NOT EXISTS `blocked_users` (
                `user_id` INTEGER NOT NULL,
                `guild_id` INTEGER NOT NULL,
                `blocked_user_id` INTEGER NOT NULL,
                PRIMARY KEY (`user_id`, `guild_id`, `blocked_user_id`)
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
        ListRedirects,
        AddIgnoredUser,
        IsIgnoredUser,
        RemoveIgnoredUser,
        ListIgnoredUserChannels,
        AddBlockedUser,
        IsBlockedUser,
        RemoveBlockedUser,
        ListBlockedUsers
    }
}
