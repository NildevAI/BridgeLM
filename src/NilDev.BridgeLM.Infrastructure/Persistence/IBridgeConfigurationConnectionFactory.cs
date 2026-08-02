namespace NilDev.BridgeLM.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;

public interface IBridgeConfigurationConnectionFactory
{
    SqliteConnection CreateConnection();
}