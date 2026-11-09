namespace ShipLean.Contracts.Agents;

// Large tool arguments an agent sends from a file: create_payload_upload issues a one-time upload URL, the agent's
// `curl -T` stores the body, and the tool is then called with the payload id instead of the arguments.
// The payload id is not a credential: every lookup is bound to the user, the agent authorization and the tool that asked
// for it. The upload token is a credential, shown once in the upload URL and stored only as its SHA-256 hash.
public interface IToolPayloads
{
    // How long the upload URL lives after it is issued.
    static readonly TimeSpan UploadWindow = TimeSpan.FromMinutes(10);

    // How long an uploaded payload can be used, counted from the upload.
    static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(60);

    // The largest body an upload stores (2 MiB).
    const int MaxBytes = 2 * 1024 * 1024;

    Task<IssuedPayload> IssueAsync(NewToolPayload payload, CancellationToken cancellationToken = default);

    // Stores the body once. ApiException.NotFound("That upload link is not valid.") for any token that is not live (unknown,
    // past UploadBy, already uploaded, authorization revoked, user disabled or security stamp changed); ApiException.Validation
    // for an empty body, a body over MaxBytes, invalid UTF-8, or, when the payload has no field, anything but one JSON object.
    Task<UploadedPayloadDto> UploadAsync(string token, Stream body, CancellationToken cancellationToken = default);

    // Null unless the payload was uploaded, has not expired, and belongs to this user, this authorization (both null for a
    // gateway caller) and this tool, the authorization is not revoked, the user is not disabled and their security stamp is
    // still the authorization's (the rules TransferTokens.FindAsync applies). The caller does not say which.
    Task<ToolPayload?> FindAsync(string payloadId, string tool, Guid userId, Guid? authorizationId, CancellationToken cancellationToken = default);
}
