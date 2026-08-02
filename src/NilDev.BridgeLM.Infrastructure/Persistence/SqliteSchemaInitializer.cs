namespace NilDev.BridgeLM.Infrastructure.Persistence;

public sealed class SqliteSchemaInitializer(
    ISqliteConnectionFactory runtimeConnectionFactory,
    IBridgeConfigurationConnectionFactory configurationConnectionFactory)
{
    private const string RequestLogSchemaSql = """
        CREATE TABLE IF NOT EXISTS request_logs (
            id TEXT NOT NULL PRIMARY KEY,
            method TEXT NOT NULL,
            path TEXT NOT NULL,
            query_string TEXT NOT NULL,
            request_headers TEXT NOT NULL,
            request_body TEXT NOT NULL,
            backend_name TEXT NOT NULL,
            backend_url TEXT NOT NULL,
            started_at_utc TEXT NOT NULL,
            completed_at_utc TEXT NULL,
            status TEXT NOT NULL,
            response_status_code INTEGER NULL,
            response_headers TEXT NULL,
            response_body TEXT NULL,
            duration_ms INTEGER NULL,
            error TEXT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_request_logs_started_at_utc ON request_logs(started_at_utc DESC);
        """;

    private const string ConfigurationSchemaSql = """
        CREATE TABLE IF NOT EXISTS bridge_configurations (
            name TEXT NOT NULL PRIMARY KEY COLLATE NOCASE,
            backend_name TEXT NOT NULL,
            backend_base_url TEXT NOT NULL,
            api_key_header TEXT NOT NULL,
            api_key TEXT NOT NULL,
            default_headers TEXT NOT NULL,
            storage_connection_string TEXT NOT NULL,
            recent_request_limit INTEGER NOT NULL,
            is_default INTEGER NOT NULL DEFAULT 0,
            created_at_utc TEXT NOT NULL,
            updated_at_utc TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_bridge_configurations_default ON bridge_configurations(is_default DESC, name COLLATE NOCASE ASC);
        """;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await InitializeConfigurationCatalogAsync(cancellationToken);
        await InitializeRequestLogStoreAsync(cancellationToken);
    }

    public async Task InitializeConfigurationCatalogAsync(CancellationToken cancellationToken)
    {
        await using var connection = configurationConnectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = ConfigurationSchemaSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task InitializeRequestLogStoreAsync(CancellationToken cancellationToken)
    {
        await using var connection = runtimeConnectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = RequestLogSchemaSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
