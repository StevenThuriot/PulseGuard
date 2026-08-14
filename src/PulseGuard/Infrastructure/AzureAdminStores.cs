using Azure;
using Azure.Data.Tables;
using PulseGuard.Entities;
using PulseGuard.Storage.Abstractions.Administration;
using TableStorage.Linq;

namespace PulseGuard.Infrastructure;

internal sealed class AzureCredentialAdministrationStore(PulseContext context) : ICredentialAdministrationStore
{
    public async Task<IReadOnlyList<StoredCredential>> GetAllAsync(CancellationToken cancellationToken)
    {
        var values = await context.Credentials.ToListAsync(cancellationToken);
        return values.Select(ToStored).Where(x => x is not null).Select(x => x!).ToList();
    }

    public async Task<StoredCredential?> GetAsync(string type, string id, CancellationToken cancellationToken)
    {
        return type switch
        {
            nameof(CredentialType.Basic) => ToStored(await context.Credentials.FindBasicCredentialsAsync(id, cancellationToken)),
            nameof(CredentialType.ApiKey) => ToStored(await context.Credentials.FindApiKeyCredentialsAsync(id, cancellationToken)),
            nameof(CredentialType.OAuth2) => ToStored(await context.Credentials.FindOAuth2CredentialsAsync(id, cancellationToken)),
            _ => null
        };
    }

    public async Task<StorageOperationResult> CreateAsync(CreateCredentialCommand command, CancellationToken cancellationToken)
    {
        try
        {
            switch (command.Credential.Type)
            {
                case nameof(CredentialType.Basic): await context.Credentials.AddEntityAsync((BasicCredentials)ToEntity(command.Credential), cancellationToken); break;
                case nameof(CredentialType.ApiKey): await context.Credentials.AddEntityAsync((ApiKeyCredentials)ToEntity(command.Credential), cancellationToken); break;
                case nameof(CredentialType.OAuth2): await context.Credentials.AddEntityAsync((OAuth2Credentials)ToEntity(command.Credential), cancellationToken); break;
                default: return StorageOperationResult.Missing();
            }

            return StorageOperationResult.Success();
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return StorageOperationResult.Conflicted();
        }
    }

    public async Task<StorageOperationResult> UpdateAsync(UpdateCredentialCommand command, CancellationToken cancellationToken)
    {
        try
        {
            switch (command.Credential.Type)
            {
                case nameof(CredentialType.Basic): await context.Credentials.UpdateEntityAsync((BasicCredentials)ToEntity(command.Credential), ETag.All, TableUpdateMode.Merge, cancellationToken); break;
                case nameof(CredentialType.ApiKey): await context.Credentials.UpdateEntityAsync((ApiKeyCredentials)ToEntity(command.Credential), ETag.All, TableUpdateMode.Merge, cancellationToken); break;
                case nameof(CredentialType.OAuth2): await context.Credentials.UpdateEntityAsync((OAuth2Credentials)ToEntity(command.Credential), ETag.All, TableUpdateMode.Merge, cancellationToken); break;
                default: return StorageOperationResult.Missing();
            }

            return StorageOperationResult.Success();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return StorageOperationResult.Missing();
        }
        catch (RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412)
        {
            return StorageOperationResult.Conflicted();
        }
    }

    public async Task<StorageOperationResult> DeleteAsync(string type, string id, CancellationToken cancellationToken)
    {
        StoredCredential? existing = await GetAsync(type, id, cancellationToken);
        if (existing is null)
        {
            return StorageOperationResult.Missing();
        }

        try
        {
            switch (type)
            {
                case nameof(CredentialType.Basic): await context.Credentials.DeleteBasicCredentialsAsync(id, cancellationToken); break;
                case nameof(CredentialType.ApiKey): await context.Credentials.DeleteApiKeyCredentialsAsync(id, cancellationToken); break;
                case nameof(CredentialType.OAuth2): await context.Credentials.DeleteOAuth2CredentialsAsync(id, cancellationToken); break;
                default: return StorageOperationResult.Missing();
            }

            return StorageOperationResult.Success();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return StorageOperationResult.Missing();
        }
    }

    private static StoredCredential? ToStored(object? value) => value switch
    {
        BasicCredentials x => new(x.Id, nameof(CredentialType.Basic), x.Username, x.Password, null, null, null, null),
        ApiKeyCredentials x => new(x.Id, nameof(CredentialType.ApiKey), null, x.ApiKey, x.Header, null, null, null),
        OAuth2Credentials x => new(x.Id, nameof(CredentialType.OAuth2), null, x.ClientSecret, null, x.TokenEndpoint, x.ClientId, x.Scopes),
        _ => null
    };

    private static object ToEntity(StoredCredential x) => x.Type switch
    {
        nameof(CredentialType.Basic) => new BasicCredentials { Id = x.Id, Username = x.Username ?? string.Empty, Password = x.Secret ?? string.Empty },
        nameof(CredentialType.ApiKey) => new ApiKeyCredentials { Id = x.Id, Header = x.Header ?? string.Empty, ApiKey = x.Secret ?? string.Empty },
        nameof(CredentialType.OAuth2) => new OAuth2Credentials { Id = x.Id, TokenEndpoint = x.TokenEndpoint ?? string.Empty, ClientId = x.ClientId ?? string.Empty, ClientSecret = x.Secret ?? string.Empty, Scopes = x.Scopes },
        _ => throw new ArgumentOutOfRangeException(nameof(x), x.Type, "Unsupported credential type.")
    };
}

internal sealed class AzureUserAdministrationStore(PulseContext context) : IUserAdministrationStore
{
    public async Task<IReadOnlyList<StoredUser>> GetAllAsync(CancellationToken cancellationToken)
        => (await context.Settings.WhereUser().ToListAsync(cancellationToken)).Select(ToStored).Where(x => x is not null).Select(x => x!).ToList();

    public async Task<StoredUser?> GetAsync(string id, CancellationToken cancellationToken)
        => ToStored(await context.Settings.FindUserAsync(id, cancellationToken));

    public async Task<StorageOperationResult> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        try { await context.Settings.AddEntityAsync(ToEntity(command.User), cancellationToken); return StorageOperationResult.Success(); }
        catch (RequestFailedException ex) when (ex.Status == 409) { return StorageOperationResult.Conflicted(); }
    }

    public async Task<StorageOperationResult> UpdateAsync(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        User? existing = await context.Settings.FindUserAsync(command.User.UserId, cancellationToken);
        if (existing is null)
        {
            return StorageOperationResult.Missing();
        }

        existing.Nickname = command.User.Nickname;
        existing.Roles = string.Join(',', command.User.Roles);
        try { await context.Settings.UpdateEntityAsync(existing, ETag.All, TableUpdateMode.Replace, cancellationToken); return StorageOperationResult.Success(); }
        catch (RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412) { return StorageOperationResult.Conflicted(); }
    }

    public async Task<StorageOperationResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        User? user = await context.Settings.FindUserAsync(id, cancellationToken);
        if (user is null)
        {
            return StorageOperationResult.Missing();
        }

        await context.Settings.DeleteEntityAsync(user, cancellationToken);
        return StorageOperationResult.Success();
    }

    private static StoredUser? ToStored(User? x) => x is null ? null : new StoredUser(x.UserId, x.Nickname, x.GetRoles().ToList(), x.LastVisited);
    private static User ToEntity(StoredUser x) => new() { UserId = x.UserId, Nickname = x.Nickname, Roles = string.Join(',', x.Roles), LastVisited = x.LastVisited };
}
