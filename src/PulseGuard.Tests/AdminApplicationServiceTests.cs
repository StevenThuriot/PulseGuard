using PulseGuard.Models.Admin;
using PulseGuard.Entities;
using PulseGuard.Services;
using PulseGuard.Services.Admin;
using PulseGuard.Storage.Abstractions.Administration;
using Xunit;

namespace PulseGuard.Tests;

public sealed class AdminApplicationServiceTests
{
    [Fact]
    public async Task BasicUpdate_PreservesExistingSecretWhenRequestOmitsPassword()
    {
        FakeCredentialStore store = new()
        {
            Existing = new StoredCredential("id", nameof(CredentialType.Basic), "user", "encrypted", null, null, null, null)
        };
        CredentialAdministrationService service = new(store, new EncryptionService(new Microsoft.Extensions.Options.OptionsWrapper<EncryptionOptions>(new EncryptionOptions { Password = "password" })), null!);

        StorageOperationResult result = await service.UpdateBasicAsync("id", new BasicCredentialRequest("new-user", string.Empty), store.Existing, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("encrypted", store.LastUpdate!.Credential.Secret);
        Assert.Equal("new-user", store.LastUpdate.Credential.Username);
    }

    private sealed class FakeCredentialStore : ICredentialAdministrationStore
    {
        public StoredCredential? Existing { get; init; }
        public UpdateCredentialCommand? LastUpdate { get; private set; }
        public Task<IReadOnlyList<StoredCredential>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<StoredCredential>>([]);
        public Task<StoredCredential?> GetAsync(string type, string id, CancellationToken cancellationToken) => Task.FromResult(Existing);
        public Task<StorageOperationResult> CreateAsync(CreateCredentialCommand command, CancellationToken cancellationToken) => Task.FromResult(StorageOperationResult.Success());
        public Task<StorageOperationResult> UpdateAsync(UpdateCredentialCommand command, CancellationToken cancellationToken)
        {
            LastUpdate = command;
            return Task.FromResult(StorageOperationResult.Success());
        }
        public Task<StorageOperationResult> DeleteAsync(string type, string id, CancellationToken cancellationToken) => Task.FromResult(StorageOperationResult.Success());
    }
}
