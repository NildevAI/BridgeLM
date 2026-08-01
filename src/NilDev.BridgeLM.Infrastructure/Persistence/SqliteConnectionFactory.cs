namespace NilDev.BridgeLM.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using NilDev.BridgeLM.Domain.Abstractions;

public sealed class SqliteConnectionFactory(IBridgeRuntimeSettingsStore runtimeSettingsStore) : ISqliteConnectionFactory
{
    public SqliteConnection CreateConnection()
    {
        var connectionString = runtimeSettingsStore.GetCurrent().Storage.ConnectionString;
        var builder = new SqliteConnectionStringBuilder(connectionString);

        if (!string.IsNullOrWhiteSpace(builder.DataSource))
        {
            var fullPath = Path.GetFullPath(builder.DataSource);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        return new SqliteConnection(builder.ConnectionString);
    }
}
