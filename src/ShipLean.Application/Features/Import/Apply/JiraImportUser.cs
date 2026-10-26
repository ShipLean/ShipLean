namespace ShipLean.Application.Features.Import.Apply;

// The seeded user that system-authored rows and unmatched Jira authors are attributed to (a test pins this id to the seed).
public static class JiraImportUser
{
    public static readonly Guid Id = new("00000001-0000-4000-8000-000000000012");

    public const string System = "jira";
}
