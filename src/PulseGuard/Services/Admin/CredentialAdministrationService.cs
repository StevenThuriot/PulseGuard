using PulseGuard.Models.Admin;
using PulseGuard.Entities;
using PulseGuard.Services;
using PulseGuard.Storage.Abstractions.Administration;

namespace PulseGuard.Services.Admin;

public sealed class CredentialAdministrationService(
    ICredentialAdministrationStore store,
    EncryptionService encryptionService,
    OAuth2CredentialsService oauth2CredentialsService)
{
    public Task<IReadOnlyList<StoredCredential>> GetAllAsync(CancellationToken cancellationToken)
        => store.GetAllAsync(cancellationToken);

    public Task<StoredCredential?> GetAsync(string type, string id, CancellationToken cancellationToken)
        => store.GetAsync(type, id, cancellationToken);

    public Task<StorageOperationResult> CreateOAuth2Async(string id, OAuth2CredentialRequest request, CancellationToken cancellationToken)
        => store.CreateAsync(new CreateCredentialCommand(new StoredCredential(
            id,
            nameof(CredentialType.OAuth2),
            null,
            encryptionService.Encrypt(request.ClientSecret),
            null,
            request.TokenEndpoint,
            request.ClientId,
            request.Scopes)), cancellationToken);

    public Task<StorageOperationResult> UpdateOAuth2Async(string id, OAuth2CredentialRequest request, CancellationToken cancellationToken)
        => UpdateAsync(nameof(CredentialType.OAuth2), id, request.TokenEndpoint, request.ClientId, request.ClientSecret, null, null, request.Scopes, cancellationToken);

    public async Task<StorageOperationResult> DeleteAsync(string type, string id, CancellationToken cancellationToken)
    {
        StoredCredential? existing = await store.GetAsync(type, id, cancellationToken);
        StorageOperationResult result = await store.DeleteAsync(type, id, cancellationToken);
        if (result.Succeeded && type.Equals(nameof(CredentialType.OAuth2), StringComparison.OrdinalIgnoreCase) && existing is not null)
        {
            oauth2CredentialsService.Purge(existing.Id);
        }

        return result;
    }

    public Task<StorageOperationResult> CreateBasicAsync(string id, BasicCredentialRequest request, CancellationToken cancellationToken)
        => store.CreateAsync(new CreateCredentialCommand(new StoredCredential(
            id,
            nameof(CredentialType.Basic),
            request.Username ?? string.Empty,
            encryptionService.Encrypt(request.Password),
            null,
            null,
            null,
            null)), cancellationToken);

    public Task<StorageOperationResult> UpdateBasicAsync(string id, BasicCredentialRequest request, StoredCredential? existing, CancellationToken cancellationToken)
        => store.UpdateAsync(new UpdateCredentialCommand(new StoredCredential(
            id,
            nameof(CredentialType.Basic),
            request.Username ?? string.Empty,
            string.IsNullOrEmpty(request.Password) ? existing?.Secret : encryptionService.Encrypt(request.Password),
            null,
            null,
            null,
            null)), cancellationToken);

    public Task<StorageOperationResult> CreateApiKeyAsync(string id, ApiKeyCredentialRequest request, CancellationToken cancellationToken)
        => store.CreateAsync(new CreateCredentialCommand(new StoredCredential(
            id,
            nameof(CredentialType.ApiKey),
            null,
            encryptionService.Encrypt(request.ApiKey),
            request.Header,
            null,
            null,
            null)), cancellationToken);

    public Task<StorageOperationResult> UpdateApiKeyAsync(string id, ApiKeyCredentialRequest request, StoredCredential? existing, CancellationToken cancellationToken)
        => store.UpdateAsync(new UpdateCredentialCommand(new StoredCredential(
            id,
            nameof(CredentialType.ApiKey),
            null,
            string.IsNullOrEmpty(request.ApiKey) ? existing?.Secret : encryptionService.Encrypt(request.ApiKey),
            request.Header,
            null,
            null,
            null)), cancellationToken);

    private async Task<StorageOperationResult> UpdateAsync(
        string type,
        string id,
        string tokenEndpoint,
        string clientId,
        string clientSecret,
        string? username,
        string? header,
        string? scopes,
        CancellationToken cancellationToken)
    {
        StoredCredential? existing = await store.GetAsync(type, id, cancellationToken);
        return await store.UpdateAsync(new UpdateCredentialCommand(new StoredCredential(
                id,
                type,
                username,
                string.IsNullOrEmpty(clientSecret) ? existing?.Secret : encryptionService.Encrypt(clientSecret),
                header,
                tokenEndpoint,
                clientId,
                scopes)), cancellationToken);
    }
}
