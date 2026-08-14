using PulseGuard.Agents;
using PulseGuard.Checks;
using PulseGuard.Entities;
using PulseGuard.Infrastructure;
using PulseGuard.Models;
using PulseGuard.Models.Admin;
using PulseGuard.Services;
using PulseGuard.Services.Admin;
using PulseGuard.Storage.Abstractions.Administration;
using System.Security.Claims;

namespace PulseGuard.Routes;

public static class AdminRoutes
{
    private const string UserEndpoint = "api/1.0/user";

    extension(IEndpointRouteBuilder builder)
    {
        public void MapAdministration(bool authorized)
        {
            if (!authorized)
            {
                EmptyUserInfo emptyUserInfoInstance = new();
                builder.MapGet(UserEndpoint, () => emptyUserInfoInstance);
                return;
            }

            builder.MapGet(UserEndpoint, static (ClaimsPrincipal user) => new UserInfo(
                    user.Identity?.Name,
                    user.FindFirstValue("firstname"),
                    user.FindFirstValue("lastname"),
                    user.Identities.SelectMany(i => i.FindAll(i.RoleClaimType)).Select(r => r.Value)
            ));

            var appGroup = builder.MapGroup("api/1.0/admin").RequireAuthorization(AuthSetup.AdministratorPolicy);

            var configurations = appGroup.MapGroup("configurations").WithTags("Admin", "Overview");
            configurations.CreateOverviewMappings();
            configurations.MapGroup("pulse").WithTags("Admin", "PulseConfigurations").CreateNormalMappings();
            configurations.MapGroup("agent").WithTags("Admin", "AgentConfigurations").CreateAgentMappings();

            appGroup.MapGroup("webhooks").WithTags("Admin", "Webhooks").CreateWebhookMappings();
            appGroup.MapGroup("users").WithTags("Admin", "Users").CreateUserMappings();
            appGroup.MapGroup("credentials").WithTags("Admin", "Credentials").CreateCredentialMappings();
        }

        private void CreateCredentialMappings()
        {
             builder.MapGet("ids", static async (CredentialAdministrationService service, CancellationToken token) =>
             {
                 var credentials = await service.GetAllAsync(token);
                 return credentials.Select(x => new CredentialOverview(Enum.Parse<CredentialType>(x.Type, true), x.Id));
             });

            var creds = builder.MapGroup("").RequireAuthorization(AuthSetup.CredentialsPolicy);

             creds.MapGet("", static async (CredentialAdministrationService service, CancellationToken token) =>
             {
                 var credentials = await service.GetAllAsync(token);
                  var entries = credentials.Select<StoredCredential, CredentialEntry>(x => x.Type switch
                 {
                     nameof(CredentialType.OAuth2) => new OAuth2CredentialEntry(x.Id, x.TokenEndpoint!, x.ClientId!, x.Scopes),
                     nameof(CredentialType.Basic) => new BasicCredentialEntry(x.Id, x.Username),
                     nameof(CredentialType.ApiKey) => new ApiKeyCredentialEntry(x.Id, x.Header!),
                     _ => throw new InvalidOperationException($"Unsupported credential type '{x.Type}'.")
                 });
                 return Results.Ok(entries);
             });

            creds.MapOAuth2Auth();
            creds.MapBasicAuth();
            creds.MapApiKeyAuth();
        }

        private void MapOAuth2Auth()
        {
            var oauth2 = builder.MapGroup("oauth2");
             oauth2.MapDelete("{id}", static async (string id, CredentialAdministrationService service, CancellationToken token) =>
             {
                 await service.DeleteAsync(nameof(CredentialType.OAuth2), id, token);
                 return Results.NoContent();
             });

             oauth2.MapPost("{id}", static async (string id, OAuth2CredentialRequest request, CredentialAdministrationService service, CancellationToken token) =>
             {
                 await service.CreateOAuth2Async(id, request, token);
                 return Results.NoContent();
             });

             oauth2.MapPut("{id}", static async (string id, OAuth2CredentialRequest request, CredentialAdministrationService service, CancellationToken token) =>
             {
                 await service.UpdateOAuth2Async(id, request, token);
                 return Results.NoContent();
             });
        }

        private void MapBasicAuth()
        {
            var basic = builder.MapGroup("basic");
             basic.MapDelete("{id}", static async (string id, CredentialAdministrationService service, CancellationToken token) =>
             {
                 await service.DeleteAsync(nameof(CredentialType.Basic), id, token);
                return Results.NoContent();
            });

             basic.MapPost("{id}", static async (string id, BasicCredentialRequest request, CredentialAdministrationService service, CancellationToken token) =>
             {
                 await service.CreateBasicAsync(id, request, token);
                return Results.NoContent();
            });

             basic.MapPut("{id}", static async (string id, BasicCredentialRequest request, CredentialAdministrationService service, CancellationToken token) =>
             {
                 StoredCredential? existing = await service.GetAsync(nameof(CredentialType.Basic), id, token);
                 await service.UpdateBasicAsync(id, request, existing, token);
                return Results.NoContent();
            });
        }

        private void MapApiKeyAuth()
        {
            var apikey = builder.MapGroup("apikey");
             apikey.MapDelete("{id}", static async (string id, CredentialAdministrationService service, CancellationToken token) =>
             {
                 await service.DeleteAsync(nameof(CredentialType.ApiKey), id, token);
                return Results.NoContent();
            });

             apikey.MapPost("{id}", static async (string id, ApiKeyCredentialRequest request, CredentialAdministrationService service, CancellationToken token) =>
             {
                 await service.CreateApiKeyAsync(id, request, token);
                return Results.NoContent();
            });

             apikey.MapPut("{id}", static async (string id, ApiKeyCredentialRequest request, CredentialAdministrationService service, CancellationToken token) =>
             {
                 StoredCredential? existing = await service.GetAsync(nameof(CredentialType.ApiKey), id, token);
                 await service.UpdateApiKeyAsync(id, request, existing, token);
                return Results.NoContent();
            });
        }

        private void CreateUserMappings()
        {
             builder.MapGet("", static async (UserAdministrationService service, CancellationToken token) =>
             {
                 IReadOnlyList<StoredUser> users = await service.GetAllAsync(token);
                 return users.Select(x => new UserEntry(x.UserId, x.Nickname, x.Roles, x.LastVisited));
             });

             builder.MapGet("{id}", static async (string id, UserAdministrationService service, CancellationToken token) =>
             {
                 StoredUser? user = await service.GetAsync(id, token);

                if (user is null)
                {
                    return Results.NotFound();
                }

                 UserEntry result = new(user.UserId, user.Nickname, user.Roles, user.LastVisited);
                return Results.Ok(result);
            });

             builder.MapDelete("{id}", static async (string id, UserAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
             {
                 StorageOperationResult result = await service.DeleteAsync(id, token);
                 if (result.NotFound)
                {
                    return Results.NotFound();
                }

                 logger.DeletedUser(id);

                return Results.NoContent();
            });

             builder.MapPut("{id}", static async (string id, UserCreateOrUpdateRequest request, UserAdministrationService service, ILogger<Program> logger, ClaimsPrincipal currentUser, CancellationToken token) =>
            {
                if (request.Roles is not null)
                {
                    IEnumerable<string> myRoles = currentUser.Identities.SelectMany(i => i.FindAll(i.RoleClaimType)).Select(r => r.Value);
                    if (request.Roles.Except(myRoles).Any())
                    {
                        return Results.BadRequest("You're only allowed to give roles you have yourself.");
                    }
                }

                 StoredUser? user = await service.GetAsync(id, token);

                if (user is null)
                {
                    return Results.NotFound();
                }

                 StoredUser update = user with
                 {
                     Roles = (request.GetRoles() ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                     Nickname = request.Nickname
                 };

                 StorageOperationResult result = await service.UpdateAsync(update, token);
                 if (result.Conflict)
                 {
                     logger.ErrorUpdatingUser(new InvalidOperationException("User update conflicted."), user.UserId);
                     return Results.Conflict();
                 }

                 logger.UpdatedUser(user.UserId);
                 return Results.NoContent();
            });

             builder.MapPut("{id}/name", static async (string id, RenameUserRequest request, UserAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
             {
                 StorageOperationResult result = await service.UpdateNameAsync(id, request.Nickname, token);
                 if (result.NotFound)
                {
                    return Results.NotFound();
                }

                 if (result.Conflict)
                 {
                     logger.ErrorRenamingUser(new InvalidOperationException("User rename conflicted."), id);
                     return Results.Conflict();
                 }

                 logger.RenamedUser(id);
                 return Results.NoContent();
            });

             builder.MapPost("{id}", static async (string id, UserCreateOrUpdateRequest request, UserAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
             {
                  StoredUser user = new(id, request.Nickname, (request.GetRoles() ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), null);

                 StorageOperationResult result = await service.CreateAsync(user, token);
                 if (result.Conflict)
                 {
                     logger.CreatedUser(user.UserId);
                     return Results.Conflict();
                 }

                 logger.CreatedUser(user.UserId);
                 return Results.Created();
            });
        }

        private void CreateOverviewMappings()
        {
             builder.MapGet("", static async (ConfigurationOverviewService service, CancellationToken token) =>
             {
                 IReadOnlyList<StoredConfigurationOverview> entries = await service.GetAsync(token);
                 return entries.Select(x => new PulseEntry(x.Id, Enum.Parse<PulseEntryType>(x.Type, true), x.SubType, x.Group, x.Name, x.Enabled));
            });

             builder.MapPut("{id}/name", static async (string id, PulseUpdateRequest entry, PulseConfigurationAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
            {
                if (entry is { Group: not null, Name: not null })
                {
                     StorageOperationResult result = await service.UpdateIdentifierAsync(new StoredServiceIdentifier(id, entry.Group, entry.Name), token);
                     if (result.NotFound)
                     {
                         return Results.NotFound();
                     }

                     if (result.Conflict)
                     {
                         return Results.Conflict();
                     }

                    logger.UpdatedPulseEntry(id, entry.Group, entry.Name);
                    return Results.NoContent();
                }

                return Results.BadRequest();
            });
        }

        private void CreateWebhookMappings()
        {
             builder.MapGet("", static async (WebhookAdministrationService service, CancellationToken token) =>
             {
                 IReadOnlyList<StoredWebhook> webhooks = await service.GetAllAsync(token);
                 return webhooks.Select(ToWebhookEntry);
             });

             builder.MapGet("{id}", static async (string id, WebhookAdministrationService service, CancellationToken token) =>
             {
                 StoredWebhook? webhook = await service.GetAsync(id, token);

                if (webhook is null)
                {
                    return Results.NotFound();
                }

                 return Results.Ok(ToWebhookEntry(webhook));
             });

             builder.MapDelete("{id}", static async (string id, WebhookAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
             {
                 StorageOperationResult result = await service.DeleteAsync(id, token);
                 if (result.NotFound)
                {
                    return Results.NotFound();
                }

                 logger.DeletedWebhookEntry(id);

                return Results.NoContent();
            });

             builder.MapPut("{id}", static async (string id, WebhookUpdateRequest request, WebhookAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
             {
                 StoredWebhook? existing = await service.GetAsync(id, token);
                 if (existing is null)
                {
                    return Results.NotFound();
                }

                 StoredWebhook update = existing with
                 {
                     Type = request.Type.ToString(), Group = request.Group, Name = request.Name,
                     Location = request.Location, Enabled = request.Enabled,
                     CredentialType = request.Credential?.Type.ToString(), CredentialId = request.Credential?.Id
                 };
                 StorageOperationResult result = await service.UpdateAsync(update, token);
                  if (result.Conflict)
                  {
                      logger.ErrorUpdatingWebhookEntry(new InvalidOperationException("Webhook update conflicted."), id);
                      return Results.Conflict();
                  }

                  logger.UpdatedWebhookEntry(id);
                 return Results.NoContent();
            });

             builder.MapPut("{id}/{enabled}", static async (string id, bool enabled, WebhookAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
             {
                 StorageOperationResult result = await service.SetEnabledAsync(id, enabled, token);
                 if (result.NotFound)
                {
                    return Results.NotFound();
                }

                  if (result.Conflict)
                  {
                     logger.ErrorUpdatingWebhookEntry(new InvalidOperationException("Webhook update conflicted."), id);
                      return Results.Conflict();
                  }

                  logger.UpdatedWebhookEntry(id);
                 return Results.NoContent();
            });

             builder.MapPost("", static async (WebhookCreationRequest request, WebhookAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
             {
                 string id = Guid.CreateVersion7().ToString("N");
                 StorageOperationResult result = await service.CreateAsync(new StoredWebhook(id, request.Secret, request.Type.ToString(), request.Group, request.Name, request.Location, request.Enabled, request.Credential?.Type.ToString(), request.Credential?.Id), token);
                  if (result.Conflict)
                  {
                     logger.ErrorCreatingWebhookEntry(new InvalidOperationException("Webhook creation conflicted."));
                      return Results.Conflict();
                  }

                  logger.CreatedWebhookEntry(id);
                 return Results.Created();
             });

             static WebhookEntry ToWebhookEntry(StoredWebhook x) => new(x.Id, Enum.Parse<WebhookType>(x.Type, true), x.Group, x.Name, x.Location, x.Enabled, x.CredentialType is null || x.CredentialId is null ? null : new CredentialOverview(Enum.Parse<CredentialType>(x.CredentialType, true), x.CredentialId));
        }

        private void CreateAgentMappings()
        {
             builder.MapGet("{id}/{type}", static async (string id, string type, AgentConfigurationAdministrationService service, CancellationToken token) =>
             {
                 StoredAgentConfiguration? configuration = await service.GetAsync(id, type, token);

                if (configuration is null)
                {
                    return Results.NotFound();
                }

                 return Results.Ok(new PulseAgentCreationRequest()
                {
                    Location = configuration.Location,
                     ApplicationName = configuration.ApplicationName ?? string.Empty,
                     SubscriptionId = configuration.SubscriptionId ?? string.Empty,
                    BuildDefinitionId = configuration.BuildDefinitionId,
                    StageName = configuration.StageName,
                    Enabled = configuration.Enabled,
                     Headers = ParseHeaders(configuration.Headers),
                     Credential = ToCredentialOverview(configuration.CredentialType, configuration.CredentialId)
                });
            });

             builder.MapPut("{id}/{type}/{enabled}", static async (string id, string type, bool enabled, AgentConfigurationAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
             {
                 StorageOperationResult result = await service.SetEnabledAsync(id, type, enabled, token);
                 if (result.NotFound)
                {
                    return Results.NotFound();
                }

                  if (result.Conflict)
                  {
                     logger.ErrorUpdatingAgentConfigurationEnabled(new InvalidOperationException("Agent configuration update conflicted."), id, type, enabled);
                      return Results.Conflict();
                  }

                  logger.UpdatedAgentConfigurationEnabled(id, type, enabled);
                 return Results.NoContent();
            });

             builder.MapPost("{id}/{type}", static async (string id, string type, PulseAgentCreationRequest request, AgentConfigurationAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
            {
                if (request.IsInvalid(type, out string? validation))
                {
                    return Results.BadRequest(validation);
                }

                 StorageOperationResult result = await service.CreateAsync(ToStoredAgentConfiguration(id, type, request), token);
                  if (result.Conflict)
                  {
                      logger.ErrorCreatingAgentConfiguration(new InvalidOperationException("Agent configuration creation conflicted."), id, type);
                      return Results.Conflict();
                  }

                  logger.CreatedAgentConfiguration(id, type);
                 return Results.Created();
            });

             builder.MapPut("{id}/{type}", static async (string id, string type, PulseAgentCreationRequest request, AgentConfigurationAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
            {
                if (request.IsInvalid(type, out string? validation))
                {
                    return Results.BadRequest(validation);
                }

                 StorageOperationResult result = await service.UpdateAsync(ToStoredAgentConfiguration(id, type, request), token);
                  if (result.Conflict)
                  {
                      logger.ErrorUpdatingAgentConfiguration(new InvalidOperationException("Agent configuration update conflicted."), id, type);
                      return Results.Conflict();
                  }

                  logger.UpdatedAgentConfiguration(id, type);
                 return Results.Created();
            });

             builder.MapDelete("{id}/{type}", static async (string id, string type, AgentConfigurationAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
             {
                 StorageOperationResult result = await service.DeleteAsync(id, type, token);
                 if (result.NotFound)
                {
                    return Results.NotFound();
                }

                 logger.DeletedAgentConfiguration(id, type);
                return Results.NoContent();
             });

             static Dictionary<string, string> ParseHeaders(string? headers)
                 => string.IsNullOrEmpty(headers)
                     ? []
                     : headers.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(':', 2)).ToDictionary(x => x[0], x => x[1]);

             static CredentialOverview? ToCredentialOverview(string? type, string? id)
                 => type is null || id is null ? null : new CredentialOverview(Enum.Parse<CredentialType>(type, true), id);

             static StoredAgentConfiguration ToStoredAgentConfiguration(string id, string type, PulseAgentCreationRequest request)
                 => new(id, type, request.Location, request.ApplicationName, request.SubscriptionId, request.BuildDefinitionId, request.StageName, request.Enabled, PulseAgentConfiguration.CreateHeaders(request.Headers), request.Credential?.Type.ToString(), request.Credential?.Id);
        }

        private void CreateNormalMappings()
        {
             builder.MapGet("{id}", static async (string id, PulseConfigurationAdministrationService service, CancellationToken token) =>
             {
                 StoredPulseConfiguration? configuration = await service.GetAsync(id, token);

                if (configuration is null)
                {
                    return Results.NotFound();
                }

                 return Results.Ok(new PulseCreationRequest()
                {
                    Group = configuration.Group,
                    Name = configuration.Name,
                     Type = Enum.Parse<PulseCheckType>(configuration.Type, true),
                    Location = configuration.Location,
                    Timeout = configuration.Timeout,
                     DegrationTimeout = configuration.DegradationTimeout,
                    Enabled = configuration.Enabled,
                    IgnoreSslErrors = configuration.IgnoreSslErrors,
                    ComparisonValue = configuration.ComparisonValue,
                     Headers = ParseHeaders(configuration.Headers),
                     Credential = ToCredentialOverview(configuration.CredentialType, configuration.CredentialId)
                });
            });

             builder.MapPost("", static async (PulseCreationRequest request, PulseConfigurationAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
            {
                if (request.Type is PulseCheckType.Json or PulseCheckType.Contains && string.IsNullOrWhiteSpace(request.ComparisonValue))
                {
                    return Results.BadRequest($"ComparisonValue is required for {request.Type} type.");
                }

                if (request.DegrationTimeout >= request.Timeout)
                {
                    return Results.BadRequest("DegrationTimeout must be less than Timeout.");
                }

                 StoredServiceIdentifier? identifier = await service.ReserveIdentifierAsync(request.Group, request.Name, token);
                  if (identifier is null)
                  {
                      logger.ErrorCreatingNormalConfiguration(new InvalidOperationException("Service identifier already exists."));
                      return Results.Conflict();
                  }

                  StoredPulseConfiguration config = ToStoredPulseConfiguration(identifier.Id, request);
                 StorageOperationResult result = await service.CreateAsync(config, token);
                  if (result.Conflict)
                  {
                      logger.ErrorCreatingNormalConfiguration(new InvalidOperationException("Pulse configuration creation conflicted."));
                      return Results.Conflict();
                  }

                  logger.CreatedNormalConfiguration(identifier.Id, request.Type.ToString());
                 return Results.Created();
            });

             builder.MapPut("{id}", static async (string id, PulseCreationRequest request, PulseConfigurationAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
            {
                if (request.Type is PulseCheckType.Json or PulseCheckType.Contains && string.IsNullOrWhiteSpace(request.ComparisonValue))
                {
                    return Results.BadRequest($"ComparisonValue is required for {request.Type} type.");
                }

                 StorageOperationResult result = await service.UpdateAsync(ToStoredPulseConfiguration(id, request), token);
                  if (result.NotFound)
                  {
                      return Results.NotFound();
                  }

                  if (result.Conflict)
                 {
                     logger.ErrorUpdatingNormalConfiguration(new InvalidOperationException("Pulse configuration update conflicted."), id, request.Type.ToString());
                     return Results.Conflict();
                 }

                  logger.UpdatedNormalConfiguration(id, request.Type.ToString());
                 return Results.Created();
            });

             builder.MapPut("{id}/{enabled}", static async (string id, bool enabled, PulseConfigurationAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
             {
                 StorageOperationResult result = await service.SetEnabledAsync(id, enabled, token);
                 if (result.NotFound)
                {
                    return Results.NotFound();
                }

                  if (result.Conflict)
                  {
                      logger.ErrorUpdatingNormalConfigurationEnabled(new InvalidOperationException("Pulse configuration update conflicted."), id, enabled);
                      return Results.Conflict();
                  }

                  logger.UpdatedNormalConfigurationEnabled(id, enabled);
                 return Results.NoContent();
            });

             builder.MapDelete("{id}", static async (string id, PulseConfigurationAdministrationService service, ILogger<Program> logger, CancellationToken token) =>
             {
                 StorageOperationResult result = await service.DeleteAsync(id, token);
                 if (result.NotFound)
                {
                    return Results.NotFound();
                }

                 logger.DeletedNormalConfiguration(id);
                return Results.NoContent();
             });

             static Dictionary<string, string> ParseHeaders(string? headers)
                 => string.IsNullOrEmpty(headers)
                     ? []
                     : headers.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(':', 2)).ToDictionary(x => x[0], x => x[1]);

             static CredentialOverview? ToCredentialOverview(string? type, string? id)
                 => type is null || id is null ? null : new CredentialOverview(Enum.Parse<CredentialType>(type, true), id);

             static StoredPulseConfiguration ToStoredPulseConfiguration(string id, PulseCreationRequest request)
                 => new(id, request.Group, request.Name, request.Type.ToString(), request.Location, request.Timeout, request.DegrationTimeout, request.Enabled, request.IgnoreSslErrors, request.ComparisonValue, PulseConfiguration.CreateHeaders(request.Headers), request.Credential?.Type.ToString(), request.Credential?.Id);
        }
    }
}
