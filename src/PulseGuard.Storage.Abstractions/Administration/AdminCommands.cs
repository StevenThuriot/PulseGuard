namespace PulseGuard.Storage.Abstractions.Administration;

public sealed record CreateCredentialCommand(StoredCredential Credential);
public sealed record UpdateCredentialCommand(StoredCredential Credential);

public sealed record CreateUserCommand(StoredUser User);
public sealed record UpdateUserCommand(StoredUser User);

public sealed record CreatePulseConfigurationCommand(StoredPulseConfiguration Configuration);
public sealed record UpdatePulseConfigurationCommand(StoredPulseConfiguration Configuration);

public sealed record CreateAgentConfigurationCommand(StoredAgentConfiguration Configuration);
public sealed record UpdateAgentConfigurationCommand(StoredAgentConfiguration Configuration);

public sealed record CreateWebhookCommand(StoredWebhook Webhook);
public sealed record UpdateWebhookCommand(StoredWebhook Webhook);

public sealed record UpdateServiceIdentifierCommand(StoredServiceIdentifier Identifier);
