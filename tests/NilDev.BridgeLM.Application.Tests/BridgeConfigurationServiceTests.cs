namespace NilDev.BridgeLM.Application.Tests;

using FluentAssertions;
using Microsoft.Extensions.Options;
using NilDev.BridgeLM.Application.Services;
using NilDev.BridgeLM.Domain.Abstractions;
using NilDev.BridgeLM.Domain.Models;
using Xunit;

public sealed class BridgeConfigurationServiceTests
{
    [Fact]
    public async Task InitializeAsync_SeedsActiveConfiguration_AndActivatesIt()
    {
        var runtimeStore = new TestRuntimeSettingsStore(CreateOptions("SeedBackend", "seed-secret"));
        var configurationStore = new InMemoryConfigurationStore();
        var service = CreateService(runtimeStore, configurationStore, CreateOptions("SeedBackend", "seed-secret"));

        await service.InitializeAsync(CancellationToken.None);

        service.GetActiveConfigurationName().Should().Be("SeedBackend");
        service.GetActiveConfiguration().BackendName.Should().Be("SeedBackend");

        var configurations = await service.ListAsync(CancellationToken.None);
        configurations.Should().ContainSingle();
        configurations[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateActiveConfigurationAsync_PreservesSecret_WhenApiKeyIsNotProvided()
    {
        var runtimeStore = new TestRuntimeSettingsStore(CreateOptions("Active", "secret-value"));
        var configurationStore = new InMemoryConfigurationStore();
        var service = CreateService(runtimeStore, configurationStore, CreateOptions("Active", "secret-value"));
        await service.InitializeAsync(CancellationToken.None);

        var updated = await service.UpdateActiveConfigurationAsync(
            new BridgeConfigurationUpdate
            {
                BackendBaseUrl = "http://localhost:8080/",
                RecentRequestLimit = 25
            },
            CancellationToken.None);

        updated.BackendBaseUrl.Should().Be("http://localhost:8080/");
        updated.RecentRequestLimit.Should().Be(25);
        updated.HasApiKey.Should().BeTrue();

        var active = await service.GetAsync("Active", CancellationToken.None);
        active.Should().NotBeNull();
        active!.Configuration.HasApiKey.Should().BeTrue();
        runtimeStore.GetCurrent().Backend.ApiKey.Should().Be("secret-value");
    }

    [Fact]
    public async Task CreateSelectRenameDuplicateAndDeleteAsync_ManageNamedConfigurations()
    {
        var runtimeStore = new TestRuntimeSettingsStore(CreateOptions("Primary", "secret-value"));
        var configurationStore = new InMemoryConfigurationStore();
        var service = CreateService(runtimeStore, configurationStore, CreateOptions("Primary", "secret-value"));
        await service.InitializeAsync(CancellationToken.None);

        var created = await service.CreateAsync(
            new BridgeNamedConfigurationCreate
            {
                Name = "Secondary",
                BackendName = "Secondary Backend",
                BackendBaseUrl = "http://secondary.local/",
                RecentRequestLimit = 32
            },
            CancellationToken.None);

        created.Name.Should().Be("Secondary");
        created.IsActive.Should().BeFalse();

        var selected = await service.SelectAsync("Secondary", CancellationToken.None);
        selected.IsActive.Should().BeTrue();
        service.GetActiveConfiguration().BackendName.Should().Be("Secondary Backend");

        var renamed = await service.RenameAsync("Secondary", "Renamed", CancellationToken.None);
        renamed.Name.Should().Be("Renamed");
        renamed.IsActive.Should().BeTrue();

        await service.SelectAsync("Primary", CancellationToken.None);
        await service.DeleteAsync("Renamed", CancellationToken.None);

        var configurations = await service.ListAsync(CancellationToken.None);
        configurations.Should().ContainSingle(configuration => configuration.Name == "Primary");
        configurations[0].IsActive.Should().BeTrue();
    }

    private static BridgeConfigurationService CreateService(
        TestRuntimeSettingsStore runtimeStore,
        InMemoryConfigurationStore configurationStore,
        BridgeRuntimeOptions bootstrapOptions) => new(
            runtimeStore,
            configurationStore,
            Options.Create(bootstrapOptions));

    private static BridgeRuntimeOptions CreateOptions(string backendName, string apiKey) => new()
    {
        Backend = new BridgeBackendOptions
        {
            Name = backendName,
            BaseUrl = "http://localhost:11434/",
            ApiKeyHeader = "Authorization",
            ApiKey = apiKey,
            DefaultHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        },
        Storage = new BridgeStorageOptions
        {
            ConnectionString = "Data Source=:memory:",
            RecentRequestLimit = 10
        }
    };

    private sealed class TestRuntimeSettingsStore(BridgeRuntimeOptions initial) : IBridgeRuntimeSettingsStore
    {
        private BridgeRuntimeOptions current = CloneOptions(initial);

        public BridgeRuntimeOptions GetCurrent() => CloneOptions(current);

        public void Update(BridgeRuntimeOptions options)
        {
            current = CloneOptions(options);
        }
    }

    private sealed class InMemoryConfigurationStore : IBridgeConfigurationStore
    {
        private readonly Dictionary<string, BridgeSavedConfiguration> configurations = new(StringComparer.OrdinalIgnoreCase);

        public Task EnsureInitializedAsync(BridgeSavedConfiguration seedConfiguration, CancellationToken cancellationToken)
        {
            if (configurations.Count == 0)
            {
                configurations[seedConfiguration.Name] = Clone(seedConfiguration);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BridgeSavedConfiguration>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BridgeSavedConfiguration>>(
                configurations.Values
                    .OrderBy(configuration => configuration.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(Clone)
                    .ToList());

        public Task<BridgeSavedConfiguration?> GetAsync(string name, CancellationToken cancellationToken)
        {
            configurations.TryGetValue(name, out var configuration);
            return Task.FromResult(configuration is null ? null : Clone(configuration));
        }

        public Task CreateOrUpdateAsync(BridgeSavedConfiguration configuration, CancellationToken cancellationToken)
        {
            configurations[configuration.Name] = Clone(configuration);
            return Task.CompletedTask;
        }

        public Task<bool> RenameAsync(string currentName, string newName, CancellationToken cancellationToken)
        {
            if (!configurations.Remove(currentName, out var configuration))
            {
                return Task.FromResult(false);
            }

            configurations[newName] = new BridgeSavedConfiguration
            {
                Name = newName,
                Options = CloneOptions(configuration.Options),
                IsActive = configuration.IsActive,
                CreatedAtUtc = configuration.CreatedAtUtc,
                UpdatedAtUtc = configuration.UpdatedAtUtc
            };
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(configurations.Remove(name));

        public Task<bool> SetActiveAsync(string name, CancellationToken cancellationToken)
        {
            if (!configurations.ContainsKey(name))
            {
                return Task.FromResult(false);
            }

            foreach (var key in configurations.Keys.ToList())
            {
                var configuration = configurations[key];
                configurations[key] = new BridgeSavedConfiguration
                {
                    Name = configuration.Name,
                    Options = CloneOptions(configuration.Options),
                    IsActive = string.Equals(key, name, StringComparison.OrdinalIgnoreCase),
                    CreatedAtUtc = configuration.CreatedAtUtc,
                    UpdatedAtUtc = configuration.UpdatedAtUtc
                };
            }

            return Task.FromResult(true);
        }

        private static BridgeSavedConfiguration Clone(BridgeSavedConfiguration configuration) => new()
        {
            Name = configuration.Name,
            Options = CloneOptions(configuration.Options),
            IsActive = configuration.IsActive,
            CreatedAtUtc = configuration.CreatedAtUtc,
            UpdatedAtUtc = configuration.UpdatedAtUtc
        };
    }

    private static BridgeRuntimeOptions CloneOptions(BridgeRuntimeOptions options) => new()
    {
        Backend = new BridgeBackendOptions
        {
            Name = options.Backend.Name,
            BaseUrl = options.Backend.BaseUrl,
            ApiKeyHeader = options.Backend.ApiKeyHeader,
            ApiKey = options.Backend.ApiKey,
            DefaultHeaders = new Dictionary<string, string>(options.Backend.DefaultHeaders, StringComparer.OrdinalIgnoreCase)
        },
        Storage = new BridgeStorageOptions
        {
            ConnectionString = options.Storage.ConnectionString,
            RecentRequestLimit = options.Storage.RecentRequestLimit
        }
    };
}