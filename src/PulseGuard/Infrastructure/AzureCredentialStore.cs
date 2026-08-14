using PulseGuard.Entities;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using TableStorage.Linq;

namespace PulseGuard.Infrastructure;

internal sealed class AzureCredentialStore(PulseContext context) : ICredentialStore
{
    public async Task<CredentialRecord?> GetAsync(string id, string type, CancellationToken cancellationToken)
    {
        return type switch
        {
            nameof(CredentialType.Basic) => await GetBasicAsync(id, cancellationToken),
            nameof(CredentialType.ApiKey) => await GetApiKeyAsync(id, cancellationToken),
            nameof(CredentialType.OAuth2) => await GetOAuth2Async(id, cancellationToken),
            _ => null
        };
    }

    private async Task<CredentialRecord?> GetBasicAsync(string id, CancellationToken cancellationToken)
    {
        BasicCredentials? value = await context.Credentials.FindBasicCredentialsAsync(id, cancellationToken);
        return value is null ? null : new(value.Id, nameof(CredentialType.Basic), value.Username, value.Password, null, null, null, null);
    }

    private async Task<CredentialRecord?> GetApiKeyAsync(string id, CancellationToken cancellationToken)
    {
        ApiKeyCredentials? value = await context.Credentials.FindApiKeyCredentialsAsync(id, cancellationToken);
        return value is null ? null : new(value.Id, nameof(CredentialType.ApiKey), null, value.ApiKey, value.Header, null, null, null);
    }

    private async Task<CredentialRecord?> GetOAuth2Async(string id, CancellationToken cancellationToken)
    {
        OAuth2Credentials? value = await context.Credentials.FindOAuth2CredentialsAsync(id, cancellationToken);
        return value is null ? null : new(value.Id, nameof(CredentialType.OAuth2), null, value.ClientSecret, null, value.TokenEndpoint, value.ClientId, value.Scopes);
    }
}
