using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Postgres.Configuration;
using PulseGuard.Storage.Postgres.Database;
using PulseGuard.Storage.Postgres.RabbitMq;
using PulseGuard.Storage.Postgres.Stores;

namespace PulseGuard.Storage.Postgres.DependencyInjection;

public static class PostgresStorageServiceCollectionExtensions
{
    public static IServiceCollection AddPostgresStorageProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("PulseStore");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:PulseStore is required for the PostgreSQL storage provider.");
        }

        services.AddOptions<PostgresStorageOptions>()
                .Configure(options => options.ConnectionString = connectionString)
                .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "PostgreSQL connection string is required.")
                .ValidateOnStart();

        services.AddOptions<RabbitMqOptions>()
                .Bind(configuration.GetSection("RabbitMq"))
                .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "RabbitMQ host is required.")
                .Validate(options => options.Port is > 0 and <= 65535, "RabbitMQ port must be valid.")
                .ValidateOnStart();

        services.AddPooledDbContextFactory<PulseGuardDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<PostgresSchemaInitializer>();
        services.AddSingleton<IStorageHealthCheck, PostgresStorageHealthCheck>();
        services.AddHostedService<PostgresSchemaHostedService>();
        services.AddSingleton<PulseGuard.Storage.Abstractions.Queues.IStorageWorkQueue, RabbitMqWorkQueue>();
        services.AddScoped<IServiceConfigurationStore, PostgresServiceConfigurationStore>();
        services.AddScoped<IHealthHistoryStore, PostgresHealthHistoryStore>();

        return services;
    }
}
