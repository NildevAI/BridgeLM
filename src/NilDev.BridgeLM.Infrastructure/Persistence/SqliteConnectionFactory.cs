namespace NilDev.BridgeLM.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using NilDev.BridgeLM.Domain.Abstractions;
using NilDev.BridgeLM.Domain.Models;

public sealed class SqliteConnectionFactory(
    IBridgeRuntimeSettingsStore runtimeSettingsStore,
    IOptions<BridgeRuntimeOptions> bootstrapOptions)
    : ISqliteConnectionFactory, IBridgeConfigurationConnectionFactory
{
    SqliteConnection ISqliteConnectionFactory.CreateConnection()
    {
        return CreateConnection(runtimeSettingsStore.GetCurrent().Storage.ConnectionString);
    }

    SqliteConnection IBridgeConfigurationConnectionFactory.CreateConnection()
    {
        return CreateConnection(bootstrapOptions.Value.Storage.ConnectionString);
    }

    private static SqliteConnection CreateConnection(string connectionString)
    {
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
