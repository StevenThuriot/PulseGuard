using PulseGuard.Entities;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using TableStorage.Linq;

namespace PulseGuard.Infrastructure;

internal sealed class AzureWebhookStore(PulseContext context) : IWebhookStore
{
    public async Task<IReadOnlyList<WebhookRecord>> GetEnabledAsync(CancellationToken cancellationToken)
    {
        List<Webhook> webhooks = await context.Webhooks.Where(x => x.Enabled).ToListAsync(cancellationToken);
        return webhooks.Select(x => new WebhookRecord(x.Id, x.Secret, x.Group, x.Name, x.Location, x.Enabled, x.Type.ToString(), x.AuthenticationId)).ToList();
    }
}
