namespace NilDev.BridgeLM.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using NilDev.BridgeLM.Domain.Abstractions;
using NilDev.BridgeLM.Domain.Models;

public sealed class SqliteRequestLogStore(SqliteSchemaInitializer schemaInitializer, ISqliteConnectionFactory connectionFactory) : IRequestLogStore
{
    public Task InitializeAsync(CancellationToken cancellationToken) => schemaInitializer.InitializeAsync(cancellationToken);

    public async Task AddAsync(ProxyRequestLog log, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO request_logs (
                id,
                method,
                path,
                query_string,
                request_headers,
                request_body,
                backend_name,
                backend_url,
                started_at_utc,
                completed_at_utc,
                status,
                response_status_code,
                response_headers,
                response_body,
                duration_ms,
                error)
            VALUES (
                $id,
                $method,
                $path,
                $query_string,
                $request_headers,
                $request_body,
                $backend_name,
                $backend_url,
                $started_at_utc,
                $completed_at_utc,
                $status,
                $response_status_code,
                $response_headers,
                $response_body,
                $duration_ms,
                $error);
            """;

        AddLogParameters(command, log);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CompleteAsync(
        string requestId,
        int statusCode,
        string responseHeaders,
        string responseBody,
        long durationMs,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE request_logs
            SET
                completed_at_utc = $completed_at_utc,
                status = 'Completed',
                response_status_code = $response_status_code,
                response_headers = $response_headers,
                response_body = $response_body,
                duration_ms = $duration_ms,
                error = NULL
            WHERE id = $id;
            """;

        command.Parameters.AddWithValue("$completed_at_utc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$response_status_code", statusCode);
        command.Parameters.AddWithValue("$response_headers", responseHeaders);
        command.Parameters.AddWithValue("$response_body", responseBody);
        command.Parameters.AddWithValue("$duration_ms", durationMs);
        command.Parameters.AddWithValue("$id", requestId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task FailAsync(
        string requestId,
        string error,
        long durationMs,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE request_logs
            SET
                completed_at_utc = $completed_at_utc,
                status = 'Failed',
                duration_ms = $duration_ms,
                error = $error
            WHERE id = $id;
            """;

        command.Parameters.AddWithValue("$completed_at_utc", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$duration_ms", durationMs);
        command.Parameters.AddWithValue("$error", error);
        command.Parameters.AddWithValue("$id", requestId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ProxyRequestLog?> GetAsync(string requestId, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                id,
                method,
                path,
                query_string,
                request_headers,
                request_body,
                backend_name,
                backend_url,
                started_at_utc,
                completed_at_utc,
                status,
                response_status_code,
                response_headers,
                response_body,
                duration_ms,
                error
            FROM request_logs
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", requestId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapLog(reader) : null;
    }

    public async Task<IReadOnlyList<ProxyRequestSummary>> ListRecentAsync(int limit, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                id,
                method,
                path,
                started_at_utc,
                status,
                backend_name,
                response_status_code,
                duration_ms
            FROM request_logs
            ORDER BY started_at_utc DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var results = new List<ProxyRequestSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapSummary(reader));
        }

        return results;
    }

    private static void AddLogParameters(SqliteCommand command, ProxyRequestLog log)
    {
        command.Parameters.AddWithValue("$id", log.Id);
        command.Parameters.AddWithValue("$method", log.Method);
        command.Parameters.AddWithValue("$path", log.Path);
        command.Parameters.AddWithValue("$query_string", log.QueryString);
        command.Parameters.AddWithValue("$request_headers", log.RequestHeaders);
        command.Parameters.AddWithValue("$request_body", log.RequestBody);
        command.Parameters.AddWithValue("$backend_name", log.BackendName);
        command.Parameters.AddWithValue("$backend_url", log.BackendUrl);
        command.Parameters.AddWithValue("$started_at_utc", log.StartedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$completed_at_utc", (object?)log.CompletedAtUtc?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", log.Status);
        command.Parameters.AddWithValue("$response_status_code", (object?)log.ResponseStatusCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$response_headers", (object?)log.ResponseHeaders ?? DBNull.Value);
        command.Parameters.AddWithValue("$response_body", (object?)log.ResponseBody ?? DBNull.Value);
        command.Parameters.AddWithValue("$duration_ms", (object?)log.DurationMs ?? DBNull.Value);
        command.Parameters.AddWithValue("$error", (object?)log.Error ?? DBNull.Value);
    }

    private static ProxyRequestLog MapLog(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Method = reader.GetString(1),
        Path = reader.GetString(2),
        QueryString = reader.GetString(3),
        RequestHeaders = reader.GetString(4),
        RequestBody = reader.GetString(5),
        BackendName = reader.GetString(6),
        BackendUrl = reader.GetString(7),
        StartedAtUtc = DateTimeOffset.Parse(reader.GetString(8)),
        CompletedAtUtc = reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9)),
        Status = reader.GetString(10),
        ResponseStatusCode = reader.IsDBNull(11) ? null : reader.GetInt32(11),
        ResponseHeaders = reader.IsDBNull(12) ? null : reader.GetString(12),
        ResponseBody = reader.IsDBNull(13) ? null : reader.GetString(13),
        DurationMs = reader.IsDBNull(14) ? null : reader.GetInt64(14),
        Error = reader.IsDBNull(15) ? null : reader.GetString(15)
    };

    private static ProxyRequestSummary MapSummary(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Method = reader.GetString(1),
        Path = reader.GetString(2),
        StartedAtUtc = DateTimeOffset.Parse(reader.GetString(3)),
        Status = reader.GetString(4),
        BackendName = reader.GetString(5),
        ResponseStatusCode = reader.IsDBNull(6) ? null : reader.GetInt32(6),
        DurationMs = reader.IsDBNull(7) ? null : reader.GetInt64(7)
    };
}