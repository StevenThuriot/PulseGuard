using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using PulseGuard.Entities;
using PulseGuard.Storage.Abstractions.Contracts;
using PulseGuard.Storage.Abstractions.Models;
using System.Security.Claims;
using TableStorage;

namespace PulseGuard.Infrastructure;

internal sealed class PulseAuthenticationSettings
{
    public required string Authority { get; set; }
    public required string Id { get; set; }
    public required string Secret { get; set; }
    public string? Scopes { get; set; }
    public bool UsePkce { get; set; } = true;
    public string ResponseMode { get; set; } = default!;
    public string UserIdClaim { get; set; } = default!;
    public bool UpsertUnknownUsers { get; set; } = true;
}

internal static class AuthSetup
{
    public const string AdministratorPolicy = "Administrator";
    public const string CredentialsPolicy = "Credentials";

    public static bool ConfigureAuthentication(this IServiceCollection services, ConfigurationManager configuration)
    {
        services.PostConfigure<PulseAuthenticationSettings>(options =>
        {
            if (string.IsNullOrEmpty(options.ResponseMode))
            {
                options.ResponseMode = OpenIdConnectResponseMode.FormPost;
            }

            if (string.IsNullOrEmpty(options.UserIdClaim))
            {
                options.UserIdClaim = JwtRegisteredClaimNames.Sub;
            }
        });

        var settings = configuration.GetSection("Authentication")?.Get<PulseAuthenticationSettings>();

        if (settings is null)
        {
            return false;
        }

        string? pathBase = configuration["PathBase"];
        const string accessDeniedPath = "/access-denied";
        bool upsertUnknownUsers = settings.UpsertUnknownUsers;

        services.AddAuthorization(options =>
                {
                    options.AddPolicy(AdministratorPolicy, policy => policy.RequireAuthenticatedUser().RequireRole("Administrator"));
                    options.AddPolicy(CredentialsPolicy, policy => policy.RequireAuthenticatedUser().RequireRole("Administrator").RequireRole("Credentials"));
                })
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie(options =>
                {
                    options.AccessDeniedPath = accessDeniedPath;
                    options.Cookie.HttpOnly = true;
#if !DEBUG
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
#endif
                    if (!string.IsNullOrEmpty(pathBase))
                    {
                        options.Cookie.Path = pathBase;
                    }
                })
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
                {
                    options.AccessDeniedPath = accessDeniedPath;

                    options.AuthenticationMethod = OpenIdConnectRedirectBehavior.FormPost;
                    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                    options.Authority = settings.Authority;
                    options.ClientId = settings.Id;
                    options.ClientSecret = settings.Secret;

                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.UsePkce = settings.UsePkce;

                    options.ResponseMode = settings.ResponseMode;

                    options.MapInboundClaims = false;
                    options.ClaimActions.MapAll();

                    options.TokenValidationParameters.NameClaimType = settings.UserIdClaim;

                    options.SaveTokens = true;

                    if (settings.Scopes is not null)
                    {
                        foreach (string scope in settings.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            options.Scope.Add(scope);
                        }
                    }

                    options.Events = new()
                    {
                        OnRemoteFailure = context =>
                        {
                            context.HandleResponse();
                            context.Response.Redirect(options.AccessDeniedPath);
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = ctx =>
                        {
                            if (ctx.Principal?.Identity is ClaimsIdentity identity && !string.IsNullOrEmpty(identity.Name))
                            {
                                return Enrich();

                                async Task Enrich()
                                {
                                    IUserStore userStore = ctx.HttpContext.RequestServices.GetRequiredService<IUserStore>();
                                    UserRecord? user = await userStore.GetAsync(identity.Name, ctx.HttpContext.RequestAborted);

                                    if (user is null)
                                    {
                                        if (!upsertUnknownUsers)
                                        {
                                            ctx.Fail("User does not exist.");
                                            return;
                                        }

                                        string? firstname = identity.FindFirst("firstname")?.Value;
                                        string? lastname = identity.FindFirst("lastname")?.Value;
                                        string nickname = $"{firstname} {lastname}".Trim();

                                             user = new(identity.Name, nickname.Length is 0 ? null : nickname, [], null);
                                    }
                                    else
                                    {
                                        identity.AddClaims(user.Roles.Select(r => new Claim(identity.RoleClaimType, r)));
                                    }

                                    await userStore.UpsertLastVisitedAsync(user, DateTimeOffset.UtcNow, ctx.HttpContext.RequestAborted);
                                }
                            }

                            return Task.CompletedTask;
                        }
                    };
                });

        return true;
    }
}
