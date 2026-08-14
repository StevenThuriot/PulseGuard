using PulseGuard.Storage.Azure.DependencyInjection;
using PulseGuard.Storage.Postgres.DependencyInjection;

namespace PulseGuard.Infrastructure;

internal static class StorageProviderSetup
{
    public static void ConfigureStorageProvider(this IServiceCollection services, ConfigurationManager configuration)
    {
        string provider = configuration["StorageProvider"]?.Trim() ?? throw new InvalidOperationException(
            "StorageProvider must be configured as Azure or Postgres.");

        switch (provider.ToUpperInvariant())
        {
            case "AZURE":
                services.AddAzureStorageProvider(configuration);
                break;

            case "POSTGRES":
                services.AddPostgresStorageProvider(configuration);
                break;

            default:
                throw new InvalidOperationException($"Unsupported StorageProvider '{provider}'. Expected Azure or Postgres.");
        }
    }
}
