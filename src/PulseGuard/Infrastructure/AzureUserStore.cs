using PulseGuard.Entities;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using TableStorage.Linq;

namespace PulseGuard.Infrastructure;

internal sealed class AzureUserStore(PulseContext context) : IUserStore
{
    public async Task<UserRecord?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await context.Settings.FindUserAsync(userId, cancellationToken);
        return user is null ? null : new UserRecord(user.UserId, user.Nickname, user.GetRoles().ToList(), user.LastVisited);
    }

    public Task UpsertLastVisitedAsync(UserRecord user, DateTimeOffset lastVisited, CancellationToken cancellationToken)
    {
        User entity = new() { UserId = user.UserId, Nickname = user.Nickname, Roles = string.Join(',', user.Roles), LastVisited = lastVisited };
        return context.Settings.UpsertEntityAsync(entity, cancellationToken);
    }
}
