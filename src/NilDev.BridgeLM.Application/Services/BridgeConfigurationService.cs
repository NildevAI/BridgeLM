namespace NilDev.BridgeLM.Application.Services;

using Microsoft.Extensions.Options;
using NilDev.BridgeLM.Domain.Abstractions;
using NilDev.BridgeLM.Domain.Models;

public sealed class BridgeConfigurationService(
    IBridgeRuntimeSettingsStore runtimeSettingsStore,
    IBridgeConfigurationStore configurationStore,
    IOptions<BridgeRuntimeOptions> bootstrapOptions)
{
    private readonly Lock gate = new();
    private string activeConfigurationName = string.Empty;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var seedOptions = Clone(bootstrapOptions.Value);
        var seedConfiguration = new BridgeSavedConfiguration
        {
            Name = DetermineSeedName(seedOptions),
            Options = seedOptions,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await configurationStore.EnsureInitializedAsync(seedConfiguration, cancellationToken);

        var configurations = await configurationStore.ListAsync(cancellationToken);
        var selected = configurations.FirstOrDefault(static configuration => configuration.IsActive)
            ?? configurations.FirstOrDefault()
            ?? throw new BridgeConfigurationValidationException("No proxy configurations are available.");

        lock (gate)
        {
            activeConfigurationName = selected.Name;
            runtimeSettingsStore.Update(Clone(selected.Options));
        }
    }

    public BridgeConfigurationView GetActiveConfiguration() => ToConfigurationView(runtimeSettingsStore.GetCurrent());

    public string GetActiveConfigurationName()
    {
        lock (gate)
        {
            return activeConfigurationName;
        }
    }

    public async Task<IReadOnlyList<BridgeNamedConfigurationSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var activeName = GetActiveConfigurationName();
        var configurations = await configurationStore.ListAsync(cancellationToken);
        return configurations.Select(configuration => new BridgeNamedConfigurationSummary
        {
            Name = configuration.Name,
            BackendName = configuration.Options.Backend.Name,
            BackendBaseUrl = configuration.Options.Backend.BaseUrl,
            HasApiKey = !string.IsNullOrWhiteSpace(configuration.Options.Backend.ApiKey),
            RecentRequestLimit = configuration.Options.Storage.RecentRequestLimit,
            IsActive = string.Equals(configuration.Name, activeName, StringComparison.OrdinalIgnoreCase)
        }).ToList();
    }

    public async Task<BridgeNamedConfigurationView?> GetAsync(string name, CancellationToken cancellationToken)
    {
        var configuration = await configurationStore.GetAsync(name, cancellationToken);
        return configuration is null ? null : ToNamedConfigurationView(configuration, GetActiveConfigurationName());
    }

    public async Task<BridgeNamedConfigurationView> CreateAsync(BridgeNamedConfigurationCreate create, CancellationToken cancellationToken)
    {
        var targetName = ValidateName(create.Name);
        if (await configurationStore.GetAsync(targetName, cancellationToken) is not null)
        {
            throw new BridgeConfigurationConflictException($"A proxy configuration named '{targetName}' already exists.");
        }

        var template = await ResolveTemplateAsync(create.CopyFromName, cancellationToken);
        var options = ApplyUpdate(
            template,
            new BridgeConfigurationUpdate
            {
                BackendName = create.BackendName,
                BackendBaseUrl = create.BackendBaseUrl,
                ApiKeyHeader = create.ApiKeyHeader,
                ApiKey = create.ApiKey,
                DefaultHeaders = create.DefaultHeaders,
                ConnectionString = create.ConnectionString,
                RecentRequestLimit = create.RecentRequestLimit
            });

        var now = DateTimeOffset.UtcNow;
        var saved = new BridgeSavedConfiguration
        {
            Name = targetName,
            Options = options,
            IsActive = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await configurationStore.CreateOrUpdateAsync(saved, cancellationToken);
        return ToNamedConfigurationView(saved, GetActiveConfigurationName());
    }

    public async Task<BridgeConfigurationView> UpdateActiveConfigurationAsync(
        BridgeConfigurationUpdate update,
        CancellationToken cancellationToken)
    {
        var activeName = GetRequiredActiveConfigurationName();
        await UpdateNamedConfigurationCoreAsync(activeName, update, cancellationToken);
        return GetActiveConfiguration();
    }

    public Task<BridgeNamedConfigurationView> UpdateNamedConfigurationAsync(
        string name,
        BridgeConfigurationUpdate update,
        CancellationToken cancellationToken) => UpdateNamedConfigurationCoreAsync(name, update, cancellationToken);

    public async Task<BridgeNamedConfigurationView> DuplicateAsync(
        string sourceName,
        string targetName,
        CancellationToken cancellationToken)
    {
        var source = await GetRequiredConfigurationAsync(sourceName, cancellationToken);
        var validatedTargetName = ValidateName(targetName);
        if (await configurationStore.GetAsync(validatedTargetName, cancellationToken) is not null)
        {
            throw new BridgeConfigurationConflictException($"A proxy configuration named '{validatedTargetName}' already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var duplicate = new BridgeSavedConfiguration
        {
            Name = validatedTargetName,
            Options = Clone(source.Options),
            IsActive = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await configurationStore.CreateOrUpdateAsync(duplicate, cancellationToken);
        return ToNamedConfigurationView(duplicate, GetActiveConfigurationName());
    }

    public async Task<BridgeNamedConfigurationView> RenameAsync(
        string currentName,
        string newName,
        CancellationToken cancellationToken)
    {
        var existing = await GetRequiredConfigurationAsync(currentName, cancellationToken);
        var validatedNewName = ValidateName(newName);
        if (string.Equals(existing.Name, validatedNewName, StringComparison.OrdinalIgnoreCase))
        {
            return ToNamedConfigurationView(existing, GetActiveConfigurationName());
        }

        if (await configurationStore.GetAsync(validatedNewName, cancellationToken) is not null)
        {
            throw new BridgeConfigurationConflictException($"A proxy configuration named '{validatedNewName}' already exists.");
        }

        var renamed = await configurationStore.RenameAsync(existing.Name, validatedNewName, cancellationToken);
        if (!renamed)
        {
            throw new BridgeConfigurationNotFoundException(existing.Name);
        }

        lock (gate)
        {
            if (string.Equals(activeConfigurationName, existing.Name, StringComparison.OrdinalIgnoreCase))
            {
                activeConfigurationName = validatedNewName;
            }
        }

        var updated = await GetRequiredConfigurationAsync(validatedNewName, cancellationToken);
        return ToNamedConfigurationView(updated, GetActiveConfigurationName());
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        var existing = await GetRequiredConfigurationAsync(name, cancellationToken);
        var configurations = await configurationStore.ListAsync(cancellationToken);
        if (configurations.Count <= 1)
        {
            throw new BridgeConfigurationValidationException("At least one proxy configuration must remain.");
        }

        if (string.Equals(existing.Name, GetActiveConfigurationName(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BridgeConfigurationValidationException("Select a different active configuration before deleting this one.");
        }

        var deleted = await configurationStore.DeleteAsync(existing.Name, cancellationToken);
        if (!deleted)
        {
            throw new BridgeConfigurationNotFoundException(existing.Name);
        }
    }

    public async Task<BridgeNamedConfigurationView> SelectAsync(string name, CancellationToken cancellationToken)
    {
        var configuration = await GetRequiredConfigurationAsync(name, cancellationToken);
        var activated = await configurationStore.SetActiveAsync(configuration.Name, cancellationToken);
        if (!activated)
        {
            throw new BridgeConfigurationNotFoundException(configuration.Name);
        }

        lock (gate)
        {
            activeConfigurationName = configuration.Name;
            runtimeSettingsStore.Update(Clone(configuration.Options));
        }

        return ToNamedConfigurationView(configuration, configuration.Name);
    }

    private async Task<BridgeNamedConfigurationView> UpdateNamedConfigurationCoreAsync(
        string name,
        BridgeConfigurationUpdate update,
        CancellationToken cancellationToken)
    {
        var existing = await GetRequiredConfigurationAsync(name, cancellationToken);
        var nextOptions = ApplyUpdate(existing.Options, update);
        var saved = new BridgeSavedConfiguration
        {
            Name = existing.Name,
            Options = nextOptions,
            IsActive = existing.IsActive,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await configurationStore.CreateOrUpdateAsync(saved, cancellationToken);
        if (string.Equals(existing.Name, GetActiveConfigurationName(), StringComparison.OrdinalIgnoreCase))
        {
            runtimeSettingsStore.Update(Clone(nextOptions));
        }

        return ToNamedConfigurationView(saved, GetActiveConfigurationName());
    }

    private async Task<BridgeRuntimeOptions> ResolveTemplateAsync(string? copyFromName, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(copyFromName))
        {
            var source = await GetRequiredConfigurationAsync(copyFromName, cancellationToken);
            return Clone(source.Options);
        }

        return Clone(runtimeSettingsStore.GetCurrent());
    }

    private async Task<BridgeSavedConfiguration> GetRequiredConfigurationAsync(string name, CancellationToken cancellationToken)
    {
        var configuration = await configurationStore.GetAsync(name, cancellationToken);
        return configuration ?? throw new BridgeConfigurationNotFoundException(name);
    }

    private string GetRequiredActiveConfigurationName()
    {
        lock (gate)
        {
            return string.IsNullOrWhiteSpace(activeConfigurationName)
                ? throw new BridgeConfigurationValidationException("No active proxy configuration is selected.")
                : activeConfigurationName;
        }
    }

    private static BridgeNamedConfigurationView ToNamedConfigurationView(
        BridgeSavedConfiguration configuration,
        string activeConfigurationName) => new()
    {
        Name = configuration.Name,
        Configuration = ToConfigurationView(configuration.Options),
        IsActive = string.Equals(configuration.Name, activeConfigurationName, StringComparison.OrdinalIgnoreCase),
        CreatedAtUtc = configuration.CreatedAtUtc,
        UpdatedAtUtc = configuration.UpdatedAtUtc
    };

    private static BridgeConfigurationView ToConfigurationView(BridgeRuntimeOptions current) => new()
    {
        BackendName = current.Backend.Name,
        BackendBaseUrl = current.Backend.BaseUrl,
        ApiKeyHeader = current.Backend.ApiKeyHeader,
        HasApiKey = !string.IsNullOrWhiteSpace(current.Backend.ApiKey),
        DefaultHeaders = new Dictionary<string, string>(current.Backend.DefaultHeaders, StringComparer.OrdinalIgnoreCase),
        ConnectionString = current.Storage.ConnectionString,
        RecentRequestLimit = current.Storage.RecentRequestLimit
    };

    private static string DetermineSeedName(BridgeRuntimeOptions options)
    {
        var candidate = string.IsNullOrWhiteSpace(options.Backend.Name) ? "Default" : options.Backend.Name.Trim();
        return candidate.Length == 0 ? "Default" : candidate;
    }

    private static string ValidateName(string name)
    {
        var candidate = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        if (candidate.Length == 0)
        {
            throw new BridgeConfigurationValidationException("Proxy configuration name is required.");
        }

        if (candidate.IndexOfAny(['/', '\\']) >= 0)
        {
            throw new BridgeConfigurationValidationException("Proxy configuration names cannot contain path separators.");
        }

        return candidate;
    }

    private static BridgeRuntimeOptions ApplyUpdate(BridgeRuntimeOptions current, BridgeConfigurationUpdate update) => new()
    {
        Backend = new BridgeBackendOptions
        {
            Name = string.IsNullOrWhiteSpace(update.BackendName) ? current.Backend.Name : update.BackendName,
            BaseUrl = string.IsNullOrWhiteSpace(update.BackendBaseUrl) ? current.Backend.BaseUrl : update.BackendBaseUrl,
            ApiKeyHeader = string.IsNullOrWhiteSpace(update.ApiKeyHeader) ? current.Backend.ApiKeyHeader : update.ApiKeyHeader,
            ApiKey = update.ApiKey ?? current.Backend.ApiKey,
            DefaultHeaders = update.DefaultHeaders is null
                ? new Dictionary<string, string>(current.Backend.DefaultHeaders, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(update.DefaultHeaders, StringComparer.OrdinalIgnoreCase)
        },
        Storage = new BridgeStorageOptions
        {
            ConnectionString = string.IsNullOrWhiteSpace(update.ConnectionString) ? current.Storage.ConnectionString : update.ConnectionString,
            RecentRequestLimit = update.RecentRequestLimit is > 0 ? update.RecentRequestLimit.Value : current.Storage.RecentRequestLimit
        }
    };

    private static BridgeRuntimeOptions Clone(BridgeRuntimeOptions options) => new()
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

public sealed class BridgeConfigurationNotFoundException(string configurationName)
    : InvalidOperationException($"Proxy configuration '{configurationName}' was not found.")
{
}

public sealed class BridgeConfigurationConflictException(string message) : InvalidOperationException(message)
{
}

public sealed class BridgeConfigurationValidationException(string message) : InvalidOperationException(message)
{
}