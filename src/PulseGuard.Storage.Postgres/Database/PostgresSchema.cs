namespace PulseGuard.Storage.Postgres.Database;

internal static class PostgresSchema
{
    public const string Sql = """
        create table if not exists schema_versions (
            version integer primary key,
            applied_at timestamptz not null default now()
        );

        create table if not exists services (
            id bigint generated always as identity primary key,
            sqid text not null unique,
            group_name text not null,
            name text not null,
            created_at timestamptz not null default now(),
            unique (group_name, name)
        );

        create table if not exists pulse_configurations (
            service_id bigint primary key references services(id),
            location text not null,
            check_type text not null,
            timeout_ms integer not null,
            degradation_timeout_ms integer,
            enabled boolean not null,
            ignore_ssl_errors boolean not null,
            comparison_value text,
            headers text,
            authentication_id text
        );

        create table if not exists agent_configurations (
            id bigint generated always as identity primary key,
            sqid text not null,
            type text not null,
            location text not null,
            application_name text,
            enabled boolean not null,
            headers text,
            authentication_id text,
            unique (sqid, type, application_name)
        );

        create table if not exists health_check_executions (
            id bigint generated always as identity,
            event_id uuid not null,
            service_id bigint not null references services(id),
            observed_at timestamptz not null,
            state text not null,
            elapsed_ms bigint not null,
            message text,
            error text,
            primary key (observed_at, id),
            unique (event_id)
        ) partition by range (observed_at);

        create table if not exists agent_executions (
            id bigint generated always as identity,
            event_id uuid not null,
            sqid text not null,
            observed_at timestamptz not null,
            cpu_percentage double precision,
            memory_percentage double precision,
            input_output double precision,
            primary key (observed_at, id),
            unique (event_id)
        ) partition by range (observed_at);

        create table if not exists service_state_periods (
            id bigint generated always as identity primary key,
            service_id bigint not null references services(id),
            state text not null,
            message text,
            error text,
            creation_timestamp timestamptz not null,
            last_updated_timestamp timestamptz not null,
            last_elapsed_ms bigint
        );

        create index if not exists ix_state_periods_service_updated
            on service_state_periods (service_id, last_updated_timestamp desc);

        create table if not exists service_failure_counters (
            service_id bigint primary key references services(id),
            consecutive_failures integer not null,
            updated_at timestamptz not null
        );

        create table if not exists service_daily_heatmaps (
            service_id bigint not null references services(id),
            day date not null,
            unknown_count integer not null default 0,
            healthy_count integer not null default 0,
            degraded_count integer not null default 0,
            unhealthy_count integer not null default 0,
            timed_out_count integer not null default 0,
            primary key (service_id, day)
        );

        create table if not exists deployments (
            id bigint generated always as identity primary key,
            service_id bigint not null references services(id),
            start_at timestamptz not null,
            end_at timestamptz,
            status text not null,
            author text,
            type text,
            commit_id text,
            build_number text
        );

        create index if not exists ix_executions_service_observed
            on health_check_executions (service_id, observed_at desc);
        create index if not exists ix_agent_executions_sqid_observed
            on agent_executions (sqid, observed_at desc);
        create index if not exists ix_deployments_service_start
            on deployments (service_id, start_at desc);
        """;
}
