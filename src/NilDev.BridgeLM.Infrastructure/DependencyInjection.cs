namespace NilDev.BridgeLM.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using NilDev.BridgeLM.Domain.Abstractions;
using NilDev.BridgeLM.Infrastructure.Forwarding;
using NilDev.BridgeLM.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddBridgeInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<SqliteSchemaInitializer>();
        services.AddSingleton<IRequestLogStore, SqliteRequestLogStore>();
        services.AddHttpClient<ILlmForwarder, ConfiguredLlmForwarder>();

        return services;
    }
}
