using Azure.Identity;
using Azure.ResourceManager;
using PulseGuard.Services;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Administration;
using PulseGuard.Services.Admin;
using Hosted = PulseGuard.Services.Hosted;

namespace PulseGuard.Infrastructure;

internal static class ServicesSetup
{
    public static void ConfigurePulseServices(this IServiceCollection services)
    {
        services.AddMemoryCache();

        services.AddSingleton<IdService>();
        services.AddSingleton<SignalService>();

        services.AddScoped<PulseStore>();

        services.AddSingleton<AsyncPulseStoreService>();
        services.AddSingleton<WebhookService>();

        services.AddSingleton(_ => new ArmClient(new DefaultAzureCredential()));

        PulseEventService eventService = new();
        services.AddSingleton<IPulseEventService>(eventService);
        services.AddSingleton<IPulseRegistrationService>(eventService);

        services.AddSingleton<EncryptionService>();
        services.AddScoped<OAuth2CredentialsService>();
        services.AddScoped<AuthService>();
        services.AddScoped<CredentialAdministrationService>();
        services.AddScoped<UserAdministrationService>();
        services.AddScoped<PulseConfigurationAdministrationService>();
        services.AddScoped<AgentConfigurationAdministrationService>();
        services.AddScoped<WebhookAdministrationService>();
        services.AddScoped<ConfigurationOverviewService>();

        services.AddHostedService<Hosted.PulseHostedService>();
        services.AddHostedService<Hosted.WebhookHostedService>();
        services.AddHostedService<Hosted.AsyncPulseStoreHostedService>();
    }

    public static void ConfigureAzureApplicationStorage(this IServiceCollection services)
    {
        services.AddScoped<IServiceConfigurationStore, AzureServiceConfigurationStore>();
        services.AddScoped<ICredentialStore, AzureCredentialStore>();
        services.AddScoped<IWebhookStore, AzureWebhookStore>();
        services.AddScoped<IUserStore, AzureUserStore>();
        services.AddScoped<IHealthHistoryStore, AzureHealthHistoryStore>();
        services.AddScoped<IAgentHistoryStore, AzureAgentHistoryStore>();
        services.AddScoped<IDeploymentStore, AzureDeploymentStore>();
        services.AddScoped<ICredentialAdministrationStore, AzureCredentialAdministrationStore>();
        services.AddScoped<IUserAdministrationStore, AzureUserAdministrationStore>();
        services.AddScoped<IPulseConfigurationAdministrationStore, AzurePulseConfigurationAdministrationStore>();
        services.AddScoped<IAgentConfigurationAdministrationStore, AzureAgentConfigurationAdministrationStore>();
        services.AddScoped<IWebhookAdministrationStore, AzureWebhookAdministrationStore>();
        services.AddHostedService<AzureStorageCleanupHostedService>();
    }
}
