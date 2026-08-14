using PulseGuard.Storage.Abstractions.Administration;

namespace PulseGuard.Services.Admin;

public sealed class UserAdministrationService(IUserAdministrationStore store)
{
    public Task<IReadOnlyList<StoredUser>> GetAllAsync(CancellationToken cancellationToken)
        => store.GetAllAsync(cancellationToken);

    public Task<StoredUser?> GetAsync(string id, CancellationToken cancellationToken)
        => store.GetAsync(id, cancellationToken);

    public Task<StorageOperationResult> CreateAsync(StoredUser user, CancellationToken cancellationToken)
        => store.CreateAsync(new CreateUserCommand(user), cancellationToken);

    public Task<StorageOperationResult> DeleteAsync(string id, CancellationToken cancellationToken)
        => store.DeleteAsync(id, cancellationToken);

    public Task<StorageOperationResult> UpdateAsync(StoredUser user, CancellationToken cancellationToken)
        => store.UpdateAsync(new UpdateUserCommand(user), cancellationToken);

    public async Task<StorageOperationResult> UpdateNameAsync(string id, string? nickname, CancellationToken cancellationToken)
    {
        StoredUser? existing = await store.GetAsync(id, cancellationToken);
        return existing is null
            ? StorageOperationResult.Missing()
            : await store.UpdateAsync(new UpdateUserCommand(existing with { Nickname = nickname }), cancellationToken);
    }
}
