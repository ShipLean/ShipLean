namespace ShipLean.Contracts.Errors;

// Thrown by commands and queries; the WebApi middleware turns it into an ApiError body with the code's
// HTTP status. Use the factories so the code and the status stay paired.
public sealed class ApiException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;

    public int Status => ApiErrorCodes.StatusFor(Code);

    // The argument at fault and, for ambiguous, the names to choose from. MCP tool results carry both (McpError's field
    // and candidates); the ApiError body of /api carries the field only. Null unless a factory below set them.
    public string? Field { get; init; }

    public IReadOnlyList<string>? Candidates { get; init; }

    public ApiError ToApiError() => new(Message, Code, Field);

    public static ApiException Internal() => new(ApiErrorCodes.Internal, "Something went wrong on the server.");

    public static ApiException NotFound(string message) => new(ApiErrorCodes.NotFound, message);

    public static ApiException NotFound(string message, string field) => new(ApiErrorCodes.NotFound, message) { Field = field };

    public static ApiException Conflict(string message) => new(ApiErrorCodes.Conflict, message);

    public static ApiException Validation(string message) => new(ApiErrorCodes.Validation, message);

    public static ApiException Validation(string message, string field) => new(ApiErrorCodes.Validation, message) { Field = field };

    public static ApiException SlugTaken(string message) => new(ApiErrorCodes.SlugTaken, message) { Field = "slug" };

    public static ApiException LinkInvalidUrl(string message) => new(ApiErrorCodes.LinkInvalidUrl, message) { Field = "url" };

    // `field` is `url` for a URL link and `targetKey` for a link to a work item.
    public static ApiException LinkDuplicate(string message, string field) => new(ApiErrorCodes.LinkDuplicate, message) { Field = field };

    public static ApiException NotMarkdown(string message) => new(ApiErrorCodes.NotMarkdown, message);

    public static ApiException MarkdownTooLarge(string message) => new(ApiErrorCodes.MarkdownTooLarge, message);

    // Issue intake settings: the field is the request field at fault (enabled, runAsUserId, repoUrl).
    public static ApiException RepositoryTaken(string message) => new(ApiErrorCodes.RepositoryTaken, message) { Field = "enabled" };

    public static ApiException NoRepoUrl(string message) => new(ApiErrorCodes.NoRepoUrl, message) { Field = "enabled" };

    public static ApiException NotMember(string message) => new(ApiErrorCodes.NotMember, message) { Field = "runAsUserId" };

    public static ApiException IntakeOn(string message) => new(ApiErrorCodes.IntakeOn, message) { Field = "repoUrl" };

    public static ApiException NotTaskOrBug(string message) => new(ApiErrorCodes.NotTaskOrBug, message);

    public static ApiException RepoUnreadable(string message) => new(ApiErrorCodes.RepoUnreadable, message) { Field = "repo" };

    public static ApiException BadSha(string message) => new(ApiErrorCodes.BadSha, message) { Field = "sha" };

    public static ApiException Ambiguous(string message) => new(ApiErrorCodes.Ambiguous, message);

    public static ApiException Ambiguous(string message, string field, IReadOnlyList<string> candidates)
        => new(ApiErrorCodes.Ambiguous, message) { Field = field, Candidates = candidates };

    public static ApiException OutOfScope(string message) => new(ApiErrorCodes.OutOfScope, message);

    public static ApiException Blocked(string message) => new(ApiErrorCodes.Blocked, message);

    public static ApiException NotConfigured(string message) => new(ApiErrorCodes.NotConfigured, message);

    public static ApiException NeedsConfirmation(string message) => new(ApiErrorCodes.NeedsConfirmation, message);

    public static ApiException Unauthorized(string message) => new(ApiErrorCodes.Unauthorized, message);

    public static ApiException Forbidden(string message) => new(ApiErrorCodes.Forbidden, message);

    public static ApiException SessionEnded() => new(ApiErrorCodes.SessionEnded, "Your session ended. Sign in again.");

    public static ApiException PasswordChangeRequired() => new(ApiErrorCodes.PasswordChangeRequired, "Choose your own password before you continue.");

    public static ApiException RateLimited(string message) => new(ApiErrorCodes.RateLimited, message);
}
