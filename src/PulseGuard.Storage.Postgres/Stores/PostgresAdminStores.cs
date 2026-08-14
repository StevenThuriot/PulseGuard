using Microsoft.EntityFrameworkCore;
using PulseGuard.Storage.Abstractions.Administration;
using PulseGuard.Storage.Postgres.Database;
using System.Linq.Expressions;

namespace PulseGuard.Storage.Postgres.Stores;

internal sealed class PostgresCredentialAdministrationStore(IDbContextFactory<PulseGuardDbContext> factory) : ICredentialAdministrationStore
{
    public async Task<IReadOnlyList<StoredCredential>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Credentials.Select(ToRecord()).ToListAsync(cancellationToken);
    }

    public async Task<StoredCredential?> GetAsync(string type, string id, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Credentials.Where(x => x.Type == type && x.Id == id).Select(ToRecord()).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<StorageOperationResult> CreateAsync(CreateCredentialCommand command, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        if (await context.Credentials.AnyAsync(x => x.Type == command.Credential.Type && x.Id == command.Credential.Id, cancellationToken))
        {
            return StorageOperationResult.Conflicted();
        }

        await context.Credentials.AddAsync(ToEntity(command.Credential), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return StorageOperationResult.Success();
    }

    public async Task<StorageOperationResult> UpdateAsync(UpdateCredentialCommand command, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        CredentialEntity? entity = await context.Credentials.FirstOrDefaultAsync(x => x.Type == command.Credential.Type && x.Id == command.Credential.Id, cancellationToken);
        if (entity is null)
        {
            return StorageOperationResult.Missing();
        }

        Copy(command.Credential, entity);
        await context.SaveChangesAsync(cancellationToken);
        return StorageOperationResult.Success();
    }

    public async Task<StorageOperationResult> DeleteAsync(string type, string id, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        CredentialEntity? entity = await context.Credentials.FirstOrDefaultAsync(x => x.Type == type && x.Id == id, cancellationToken);
        if (entity is null)
        {
            return StorageOperationResult.Missing();
        }

        context.Credentials.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return StorageOperationResult.Success();
    }

    private static Expression<Func<CredentialEntity, StoredCredential>> ToRecord() => x => new StoredCredential(x.Id, x.Type, x.Username, x.Secret, x.Header, x.TokenEndpoint, x.ClientId, x.Scopes);
    private static CredentialEntity ToEntity(StoredCredential x) => new() { Id = x.Id, Type = x.Type, Username = x.Username, Secret = x.Secret, Header = x.Header, TokenEndpoint = x.TokenEndpoint, ClientId = x.ClientId, Scopes = x.Scopes };
    private static void Copy(StoredCredential x, CredentialEntity entity) { entity.Username = x.Username; entity.Secret = x.Secret; entity.Header = x.Header; entity.TokenEndpoint = x.TokenEndpoint; entity.ClientId = x.ClientId; entity.Scopes = x.Scopes; }
}

internal sealed class PostgresUserAdministrationStore(IDbContextFactory<PulseGuardDbContext> factory) : IUserAdministrationStore
{
    public async Task<IReadOnlyList<StoredUser>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Users.Select(x => new StoredUser(x.UserId, x.Nickname, x.Roles, x.LastVisited)).ToListAsync(cancellationToken);
    }

    public async Task<StoredUser?> GetAsync(string id, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Users.Where(x => x.UserId == id).Select(x => new StoredUser(x.UserId, x.Nickname, x.Roles, x.LastVisited)).FirstOrDefaultAsync(cancellationToken);
    }

    public Task<StorageOperationResult> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken) => WriteAsync(command.User, false, cancellationToken);
    public Task<StorageOperationResult> UpdateAsync(UpdateUserCommand command, CancellationToken cancellationToken) => WriteAsync(command.User, true, cancellationToken);

    public async Task<StorageOperationResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        UserEntity? entity = await context.Users.FirstOrDefaultAsync(x => x.UserId == id, cancellationToken);
        if (entity is null)
        {
            return StorageOperationResult.Missing();
        }

        context.Users.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        return StorageOperationResult.Success();
    }

    private async Task<StorageOperationResult> WriteAsync(StoredUser user, bool update, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await factory.CreateDbContextAsync(cancellationToken);
        UserEntity? entity = await context.Users.FirstOrDefaultAsync(x => x.UserId == user.UserId, cancellationToken);
        if (!update && entity is not null)
        {
            return StorageOperationResult.Conflicted();
        }

        if (update && entity is null)
        {
            return StorageOperationResult.Missing();
        }

        entity ??= new UserEntity { UserId = user.UserId };
        entity.Nickname = user.Nickname;
        entity.Roles = [.. user.Roles];
        entity.LastVisited = user.LastVisited;
        context.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
        return StorageOperationResult.Success();
    }
}
