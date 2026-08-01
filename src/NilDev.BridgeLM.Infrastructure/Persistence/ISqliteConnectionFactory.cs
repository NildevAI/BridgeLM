namespace NilDev.BridgeLM.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;

public interface ISqliteConnectionFactory
{
    SqliteConnection CreateConnection();
}
