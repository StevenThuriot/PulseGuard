using Microsoft.EntityFrameworkCore;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using PulseGuard.Storage.Postgres.Database;

namespace PulseGuard.Storage.Postgres.Stores;

internal sealed class PostgresUserStore(IDbContextFactory<PulseGuardDbContext> contextFactory) : IUserStore
{
    public async Task<UserRecord?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Users.Where(x => x.UserId == userId)
            .Select(x => new UserRecord(x.UserId, x.Nickname, x.Roles, x.LastVisited))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertLastVisitedAsync(UserRecord user, DateTimeOffset lastVisited, CancellationToken cancellationToken)
    {
        await using PulseGuardDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        UserEntity entity = await context.Users.FirstOrDefaultAsync(x => x.UserId == user.UserId, cancellationToken)
            ?? new UserEntity { UserId = user.UserId, Nickname = user.Nickname, Roles = [.. user.Roles] };
        entity.LastVisited = lastVisited;
        context.Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}