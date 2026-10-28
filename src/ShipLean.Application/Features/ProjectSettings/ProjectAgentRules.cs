using System.Text.RegularExpressions;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.ProjectSettings;

// What a project's repository URL and base branch may be. Blank means unset (a null branch is main).
internal static partial class ProjectAgentRules
{
    public const string DefaultBranch = "main";
    private const int MaxUrl = 500;
    private const int MaxBranch = 200;

    // https://host/path, ssh://host/path, or the scp form git@host:path. No password in the address.
    public static string? RepoUrl(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return null;
        }
        if (text.Length > MaxUrl || text.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) || !(IsUrl(text) || ScpForm().IsMatch(text)))
        {
            throw ApiException.Validation("The repository must be a git address: https://host/org/repo, ssh://git@host/org/repo or git@host:org/repo.", "repoUrl");
        }
        return text;
    }

    public static string? BaseBranch(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return null;
        }
        if (text.Length > MaxBranch || !BranchName().IsMatch(text) || text.Contains("..", StringComparison.Ordinal) || text.Contains("//", StringComparison.Ordinal)
            || text.EndsWith('/') || text.EndsWith('.') || text.EndsWith(".lock", StringComparison.Ordinal))
        {
            throw ApiException.Validation("The base branch must be a git branch name, such as main or release/1.2.", "repoBaseBranch");
        }
        return text;
    }

    private static bool IsUrl(string text)
        => Uri.TryCreate(text, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "ssh")
           && uri.Host.Length > 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0
           && (uri.Scheme == "ssh" ? !uri.UserInfo.Contains(':') : uri.UserInfo.Length == 0);

    [GeneratedRegex(@"^[A-Za-z0-9._-]+@[A-Za-z0-9.-]+:[A-Za-z0-9._~/-]+$")]
    private static partial Regex ScpForm();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._/-]*$")]
    private static partial Regex BranchName();
}
