using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Formatting.Compact;
using System.Security.Principal;

namespace PulseGuard.Infrastructure;

internal static class TelemetrySetup
{
    private const string DefaultLogPath = "logs/pulseguard-.json";

    public static void ConfigurePulseTelemetry(this IServiceCollection services, ConfigurationManager configuration)
    {
        ApplicationTelemetryOptions options = configuration.GetSection("ApplicationTelemetry").Get<ApplicationTelemetryOptions>() ?? new();

        if (options.Enabled)
        {
            string path = string.IsNullOrWhiteSpace(options.FilePath) ? DefaultLogPath : options.FilePath;
            services.AddSingleton<ILoggerProvider>(_ => new Serilog.Extensions.Logging.SerilogLoggerProvider(
                new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .Enrich.FromLogContext()
                    .WriteTo.File(new CompactJsonFormatter(), path, rollingInterval: RollingInterval.Day)
                    .CreateLogger(),
                dispose: true));
        }
    }

    public static void UsePulseTelemetry(this WebApplication app)
    {
        app.UseMiddleware<UserIdMiddleware>();
    }

    private sealed class UserIdMiddleware(RequestDelegate next, ILogger<UserIdMiddleware> logger)
    {
        private readonly RequestDelegate _next = next;
        private readonly ILogger<UserIdMiddleware> _logger = logger;

        public async Task InvokeAsync(HttpContext context)
        {
            IIdentity? identity = context.User?.Identity;

            if (identity?.IsAuthenticated == true)
            {
                using IDisposable? scope = _logger.BeginScope(new Dictionary<string, object?>
                {
                    ["AuthenticatedUserId"] = identity.Name,
                    ["UserAgent"] = context.Request.Headers.UserAgent.ToString()
                });

                await _next(context);
                return;
            }

            await _next(context);
        }
    }

    private sealed class ApplicationTelemetryOptions
    {
        public bool Enabled { get; init; }

        public string? FilePath { get; init; }
    }
}
