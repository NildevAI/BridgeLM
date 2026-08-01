namespace NilDev.BridgeLM.Infrastructure.Persistence;

public sealed class SqliteSchemaInitializer(ISqliteConnectionFactory connectionFactory)
{
    private const string SchemaSql = """
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

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SchemaSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
