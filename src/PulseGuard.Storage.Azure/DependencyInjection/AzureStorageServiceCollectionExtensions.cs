using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PulseGuard.Storage.Abstractions.Queues;
using PulseGuard.Storage.Azure.Configuration;
using PulseGuard.Storage.Azure.Queues;

namespace PulseGuard.Storage.Azure.DependencyInjection;

public static class AzureStorageServiceCollectionExtensions
{
    public static IServiceCollection AddAzureStorageProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("PulseStore");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:PulseStore is required for the Azure storage provider.");
        }

        services.AddOptions<AzureStorageOptions>()
                .Configure(options => options.ConnectionString = connectionString)
                .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "Azure storage connection string is required.")
                .ValidateOnStart();

        services.AddSingleton<IStorageWorkQueue, AzureQueueWorkQueue>();
        return services;
    }
}
