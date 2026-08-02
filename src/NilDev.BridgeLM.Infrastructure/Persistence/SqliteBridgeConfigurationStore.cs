namespace NilDev.BridgeLM.Infrastructure.Persistence;

using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NilDev.BridgeLM.Domain.Abstractions;
using NilDev.BridgeLM.Domain.Models;

public sealed class SqliteBridgeConfigurationStore(
    SqliteSchemaInitializer schemaInitializer,
    IBridgeConfigurationConnectionFactory connectionFactory) : IBridgeConfigurationStore
{
    public async Task EnsureInitializedAsync(BridgeSavedConfiguration seedConfiguration, CancellationToken cancellationToken)
    {
        await schemaInitializer.InitializeConfigurationCatalogAsync(cancellationToken);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM bridge_configurations;";
        var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        if (count > 0)
        {
            return;
        }

        await CreateOrUpdateCoreAsync(connection, seedConfiguration, cancellationToken);
        await SetActiveCoreAsync(connection, seedConfiguration.Name, cancellationToken);
    }

    public async Task<IReadOnlyList<BridgeSavedConfiguration>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                name,
                backend_name,
                backend_base_url,
                api_key_header,
                api_key,
                default_headers,
                storage_connection_string,
                recent_request_limit,
                is_default,
                created_at_utc,
                updated_at_utc
            FROM bridge_configurations
            ORDER BY name COLLATE NOCASE;
            """;

        var results = new List<BridgeSavedConfiguration>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapConfiguration(reader));
        }

        return results;
    }

    public async Task<BridgeSavedConfiguration?> GetAsync(string name, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                name,
                backend_name,
                backend_base_url,
                api_key_header,
                api_key,
                default_headers,
                storage_connection_string,
                recent_request_limit,
                is_default,
                created_at_utc,
                updated_at_utc
            FROM bridge_configurations
            WHERE name = $name COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$name", name);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapConfiguration(reader) : null;
    }

    public async Task CreateOrUpdateAsync(BridgeSavedConfiguration configuration, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await CreateOrUpdateCoreAsync(connection, configuration, cancellationToken);
    }

    public async Task<bool> RenameAsync(string currentName, string newName, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE bridge_configurations
            SET name = $new_name
            WHERE name = $current_name COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$new_name", newName);
        command.Parameters.AddWithValue("$current_name", currentName);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteAsync(string name, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM bridge_configurations WHERE name = $name COLLATE NOCASE;";
        command.Parameters.AddWithValue("$name", name);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> SetActiveAsync(string name, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        return await SetActiveCoreAsync(connection, name, cancellationToken);
    }

    private static async Task CreateOrUpdateCoreAsync(
        SqliteConnection connection,
        BridgeSavedConfiguration configuration,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO bridge_configurations (
                name,
                backend_name,
                backend_base_url,
                api_key_header,
                api_key,
                default_headers,
                storage_connection_string,
                recent_request_limit,
                is_default,
                created_at_utc,
                updated_at_utc)
            VALUES (
                $name,
                $backend_name,
                $backend_base_url,
                $api_key_header,
                $api_key,
                $default_headers,
                $storage_connection_string,
                $recent_request_limit,
                $is_default,
                $created_at_utc,
                $updated_at_utc)
            ON CONFLICT(name) DO UPDATE SET
                backend_name = excluded.backend_name,
                backend_base_url = excluded.backend_base_url,
                api_key_header = excluded.api_key_header,
                api_key = excluded.api_key,
                default_headers = excluded.default_headers,
                storage_connection_string = excluded.storage_connection_string,
                recent_request_limit = excluded.recent_request_limit,
                is_default = excluded.is_default,
                updated_at_utc = excluded.updated_at_utc;
            """;

        command.Parameters.AddWithValue("$name", configuration.Name);
        command.Parameters.AddWithValue("$backend_name", configuration.Options.Backend.Name);
        command.Parameters.AddWithValue("$backend_base_url", configuration.Options.Backend.BaseUrl);
        command.Parameters.AddWithValue("$api_key_header", configuration.Options.Backend.ApiKeyHeader);
        command.Parameters.AddWithValue("$api_key", configuration.Options.Backend.ApiKey);
        command.Parameters.AddWithValue(
            "$default_headers",
            JsonSerializer.Serialize(
                configuration.Options.Backend.DefaultHeaders,
                PersistenceJsonSerializerContext.Default.DictionaryStringString));
        command.Parameters.AddWithValue("$storage_connection_string", configuration.Options.Storage.ConnectionString);
        command.Parameters.AddWithValue("$recent_request_limit", configuration.Options.Storage.RecentRequestLimit);
        command.Parameters.AddWithValue("$is_default", configuration.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$created_at_utc", configuration.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$updated_at_utc", configuration.UpdatedAtUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> SetActiveCoreAsync(
        SqliteConnection connection,
        string name,
        CancellationToken cancellationToken)
    {
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var clearCommand = connection.CreateCommand();
        clearCommand.Transaction = transaction;
        clearCommand.CommandText = "UPDATE bridge_configurations SET is_default = 0 WHERE is_default <> 0;";
        await clearCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var setCommand = connection.CreateCommand();
        setCommand.Transaction = transaction;
        setCommand.CommandText = "UPDATE bridge_configurations SET is_default = 1 WHERE name = $name COLLATE NOCASE;";
        setCommand.Parameters.AddWithValue("$name", name);
        var updated = await setCommand.ExecuteNonQueryAsync(cancellationToken) > 0;
        if (updated)
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        await transaction.RollbackAsync(cancellationToken);
        return false;
    }

    private static BridgeSavedConfiguration MapConfiguration(SqliteDataReader reader)
    {
        var defaultHeaders = JsonSerializer.Deserialize(
            reader.GetString(5),
            PersistenceJsonSerializerContext.Default.DictionaryStringString)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new BridgeSavedConfiguration
        {
            Name = reader.GetString(0),
            Options = new BridgeRuntimeOptions
            {
                Backend = new BridgeBackendOptions
                {
                    Name = reader.GetString(1),
                    BaseUrl = reader.GetString(2),
                    ApiKeyHeader = reader.GetString(3),
                    ApiKey = reader.GetString(4),
                    DefaultHeaders = new Dictionary<string, string>(defaultHeaders, StringComparer.OrdinalIgnoreCase)
                },
                Storage = new BridgeStorageOptions
                {
                    ConnectionString = reader.GetString(6),
                    RecentRequestLimit = reader.GetInt32(7)
                }
            },
            IsActive = reader.GetInt64(8) != 0,
            CreatedAtUtc = DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture),
            UpdatedAtUtc = DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture)
        };
    }
}