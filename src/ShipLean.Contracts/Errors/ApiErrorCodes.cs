namespace ShipLean.Contracts.Errors;

// The machine-readable error codes. `validation` (400) is a request-shape failure. `not_configured` (503) is a feature
// whose provider has not been set up. `unauthorized` (401) is a request with no signed-in user (or a failed sign-in),
// `forbidden` (403) a signed-in user without the right (an admin page, a cross-site write). `internal_error` (500) is
// what any unhandled exception becomes, with a fixed message that never leaks details. `rate_limited` (429) is a caller
// over a rate limit (the per-IP sign-in limiter). `session_ended` (401) is a session cookie that no longer counts (the password
// changed elsewhere, "sign out everywhere else", or the account was disabled); `password_change_required` (403) is a
// signed-in user who must replace their one-time password before anything else. `slug_taken` and `link_duplicate` (409)
// are a value another row already holds (a Feature's slug; a URL or work item already linked), `link_invalid_url` (400) is a
// link address that is not an http(s) URL, and `not_markdown` and `markdown_too_large` (400) are an attachment the Markdown
// view refuses; `repository_taken` (409, another project takes that GitHub repository's issues), `no_repo_url` (400, the
// project's repo_url names no github.com repository), `not_member` (400, the chosen person is not a member of the project)
// and `intake_on` (409, the repo_url can't change while issue intake is on) are the issue intake settings' refusals; each
// is a narrower `conflict` or `validation` that the UI can tell apart by code, with the field at fault (slug, url,
// targetKey, enabled, runAsUserId, repoUrl) in ApiError.Field where there is one. `not_task_or_bug` and `bad_sha` (400) and
// `repo_unreadable` (422) are the commit commands' refusals: commits go only on a Task or Bug, a sha must be 7 to 64 hex
// characters naming a commit that exists, and the instance token must be able to read the repository.
public static class ApiErrorCodes
{
    public const string NotFound = "not_found";
    public const string Ambiguous = "ambiguous";
    public const string Conflict = "conflict";
    public const string OutOfScope = "out_of_scope";
    public const string Blocked = "blocked";
    public const string NeedsConfirmation = "needs_confirmation";
    public const string Validation = "validation";
    public const string NotConfigured = "not_configured";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string RateLimited = "rate_limited";
    public const string SessionEnded = "session_ended";
    public const string PasswordChangeRequired = "password_change_required";
    public const string Internal = "internal_error";
    public const string SlugTaken = "slug_taken";
    public const string LinkInvalidUrl = "link_invalid_url";
    public const string LinkDuplicate = "link_duplicate";
    public const string NotMarkdown = "not_markdown";
    public const string MarkdownTooLarge = "markdown_too_large";
    public const string RepositoryTaken = "repository_taken";
    public const string NoRepoUrl = "no_repo_url";
    public const string NotMember = "not_member";
    public const string IntakeOn = "intake_on";
    public const string NotTaskOrBug = "not_task_or_bug";
    public const string RepoUnreadable = "repo_unreadable";
    public const string BadSha = "bad_sha";

    public static IReadOnlyList<string> All { get; } =
        [NotFound, Ambiguous, Conflict, OutOfScope, Blocked, NeedsConfirmation, Validation, NotConfigured, Unauthorized, Forbidden, RateLimited, SessionEnded, PasswordChangeRequired, Internal, SlugTaken, LinkInvalidUrl, LinkDuplicate, NotMarkdown, MarkdownTooLarge, RepositoryTaken, NoRepoUrl, NotMember, IntakeOn, NotTaskOrBug, RepoUnreadable, BadSha];

    // not_found 404; validation 400; out_of_scope and blocked 422 (understood, refused);
    // conflict, ambiguous, needs_confirmation, slug_taken and link_duplicate 409 (state must change or the caller must decide).
    public static int StatusFor(string code) => code switch
    {
        NotFound => 404,
        Validation or LinkInvalidUrl or NotMarkdown or MarkdownTooLarge or NoRepoUrl or NotMember or NotTaskOrBug or BadSha => 400,
        Unauthorized or SessionEnded => 401,
        Forbidden or PasswordChangeRequired => 403,
        RateLimited => 429,
        Internal => 500,
        NotConfigured => 503,
        OutOfScope or Blocked or RepoUnreadable => 422,
        Conflict or Ambiguous or NeedsConfirmation or SlugTaken or LinkDuplicate or RepositoryTaken or IntakeOn => 409,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown API error code."),
    };
}
