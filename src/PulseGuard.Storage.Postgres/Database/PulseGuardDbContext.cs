using Microsoft.EntityFrameworkCore;

namespace PulseGuard.Storage.Postgres.Database;

internal sealed class PulseGuardDbContext(DbContextOptions<PulseGuardDbContext> options) : DbContext(options)
{
    internal DbSet<ServiceEntity> Services => Set<ServiceEntity>();
    internal DbSet<PulseConfigurationEntity> PulseConfigurations => Set<PulseConfigurationEntity>();
    internal DbSet<AgentConfigurationEntity> AgentConfigurations => Set<AgentConfigurationEntity>();
    internal DbSet<HealthCheckExecutionEntity> HealthCheckExecutions => Set<HealthCheckExecutionEntity>();
    internal DbSet<AgentExecutionEntity> AgentExecutions => Set<AgentExecutionEntity>();
    internal DbSet<ServiceStatePeriodEntity> StatePeriods => Set<ServiceStatePeriodEntity>();
    internal DbSet<ServiceFailureCounterEntity> FailureCounters => Set<ServiceFailureCounterEntity>();
    internal DbSet<ServiceDailyHeatmapEntity> Heatmaps => Set<ServiceDailyHeatmapEntity>();
    internal DbSet<DeploymentEntity> Deployments => Set<DeploymentEntity>();
    internal DbSet<CredentialEntity> Credentials => Set<CredentialEntity>();
    internal DbSet<WebhookEntity> Webhooks => Set<WebhookEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServiceEntity>(entity =>
        {
            entity.ToTable("services");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Sqid).IsUnique();
            entity.HasIndex(x => new { x.GroupName, x.Name }).IsUnique();
            entity.Property(x => x.GroupName).HasColumnName("group_name");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<PulseConfigurationEntity>(entity =>
        {
            entity.ToTable("pulse_configurations");
            entity.HasKey(x => x.ServiceId);
            entity.Property(x => x.ServiceId).HasColumnName("service_id");
            entity.Property(x => x.CheckType).HasColumnName("check_type");
            entity.Property(x => x.TimeoutMilliseconds).HasColumnName("timeout_ms");
            entity.Property(x => x.DegradationTimeoutMilliseconds).HasColumnName("degradation_timeout_ms");
            entity.Property(x => x.IgnoreSslErrors).HasColumnName("ignore_ssl_errors");
            entity.Property(x => x.ComparisonValue).HasColumnName("comparison_value");
            entity.Property(x => x.AuthenticationId).HasColumnName("authentication_id");
            entity.HasOne(x => x.Service).WithOne(x => x.PulseConfiguration).HasForeignKey<PulseConfigurationEntity>(x => x.ServiceId);
        });

        modelBuilder.Entity<AgentConfigurationEntity>(entity =>
        {
            entity.ToTable("agent_configurations");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Sqid, x.Type, x.ApplicationName }).IsUnique();
            entity.Property(x => x.ApplicationName).HasColumnName("application_name");
            entity.Property(x => x.AuthenticationId).HasColumnName("authentication_id");
            entity.Property(x => x.SubscriptionId).HasColumnName("subscription_id");
            entity.Property(x => x.BuildDefinitionId).HasColumnName("build_definition_id");
            entity.Property(x => x.StageName).HasColumnName("stage_name");
        });

        modelBuilder.Entity<HealthCheckExecutionEntity>(entity =>
        {
            entity.ToTable("health_check_executions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.EventId).IsUnique();
            entity.HasIndex(x => new { x.ServiceId, x.ObservedAt });
            entity.Property(x => x.EventId).HasColumnName("event_id");
            entity.Property(x => x.ServiceId).HasColumnName("service_id");
            entity.Property(x => x.ObservedAt).HasColumnName("observed_at");
            entity.Property(x => x.ElapsedMilliseconds).HasColumnName("elapsed_ms");
            entity.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId);
        });

        modelBuilder.Entity<AgentExecutionEntity>(entity =>
        {
            entity.ToTable("agent_executions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.EventId).IsUnique();
            entity.HasIndex(x => new { x.Sqid, x.ObservedAt });
            entity.Property(x => x.EventId).HasColumnName("event_id");
            entity.Property(x => x.ObservedAt).HasColumnName("observed_at");
            entity.Property(x => x.CpuPercentage).HasColumnName("cpu_percentage");
            entity.Property(x => x.MemoryPercentage).HasColumnName("memory_percentage");
            entity.Property(x => x.InputOutput).HasColumnName("input_output");
        });

        modelBuilder.Entity<ServiceStatePeriodEntity>(entity =>
        {
            entity.ToTable("service_state_periods");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ServiceId, x.LastUpdatedTimestamp });
            entity.Property(x => x.ServiceId).HasColumnName("service_id");
            entity.Property(x => x.CreationTimestamp).HasColumnName("creation_timestamp");
            entity.Property(x => x.LastUpdatedTimestamp).HasColumnName("last_updated_timestamp");
            entity.Property(x => x.LastElapsedMilliseconds).HasColumnName("last_elapsed_ms");
            entity.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId);
        });

        modelBuilder.Entity<ServiceFailureCounterEntity>(entity =>
        {
            entity.ToTable("service_failure_counters");
            entity.HasKey(x => x.ServiceId);
            entity.Property(x => x.ServiceId).HasColumnName("service_id");
            entity.Property(x => x.ConsecutiveFailures).HasColumnName("consecutive_failures");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId);
        });

        modelBuilder.Entity<ServiceDailyHeatmapEntity>(entity =>
        {
            entity.ToTable("service_daily_heatmaps");
            entity.HasKey(x => new { x.ServiceId, x.Day });
            entity.Property(x => x.ServiceId).HasColumnName("service_id");
            entity.Property(x => x.UnknownCount).HasColumnName("unknown_count");
            entity.Property(x => x.HealthyCount).HasColumnName("healthy_count");
            entity.Property(x => x.DegradedCount).HasColumnName("degraded_count");
            entity.Property(x => x.UnhealthyCount).HasColumnName("unhealthy_count");
            entity.Property(x => x.TimedOutCount).HasColumnName("timed_out_count");
            entity.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId);
        });

        modelBuilder.Entity<DeploymentEntity>(entity =>
        {
            entity.ToTable("deployments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ServiceId, x.StartAt });
            entity.Property(x => x.ServiceId).HasColumnName("service_id");
            entity.Property(x => x.StartAt).HasColumnName("start_at");
            entity.Property(x => x.EndAt).HasColumnName("end_at");
            entity.Property(x => x.CommitId).HasColumnName("commit_id");
            entity.Property(x => x.BuildNumber).HasColumnName("build_number");
            entity.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceId);
        });

        modelBuilder.Entity<CredentialEntity>(entity =>
        {
            entity.ToTable("credentials");
            entity.HasKey(x => x.IdValue);
            entity.HasIndex(x => new { x.Id, x.Type }).IsUnique();
            entity.Property(x => x.IdValue).HasColumnName("id_value");
            entity.Property(x => x.TokenEndpoint).HasColumnName("token_endpoint");
            entity.Property(x => x.ClientId).HasColumnName("client_id");
        });

        modelBuilder.Entity<WebhookEntity>(entity =>
        {
            entity.ToTable("webhooks");
            entity.HasKey(x => x.IdValue);
            entity.HasIndex(x => x.Id).IsUnique();
            entity.Property(x => x.IdValue).HasColumnName("id_value");
            entity.Property(x => x.AuthenticationId).HasColumnName("authentication_id");
        });
    }
}
