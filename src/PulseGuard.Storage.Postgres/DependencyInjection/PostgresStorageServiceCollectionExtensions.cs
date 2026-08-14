using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Postgres.Configuration;
using PulseGuard.Storage.Postgres.Database;

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

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<PostgresSchemaInitializer>();
        services.AddSingleton<IStorageHealthCheck, PostgresStorageHealthCheck>();
        services.AddHostedService<PostgresSchemaHostedService>();

        return services;
    }
}
