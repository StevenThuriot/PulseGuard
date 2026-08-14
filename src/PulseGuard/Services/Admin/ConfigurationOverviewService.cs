using PulseGuard.Storage.Abstractions.Administration;

namespace PulseGuard.Services.Admin;

public sealed class ConfigurationOverviewService(IConfigurationOverviewStore store)
{
    public Task<IReadOnlyList<StoredConfigurationOverview>> GetAsync(CancellationToken cancellationToken)
        => store.GetAsync(cancellationToken);
}
