using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Shell;

// Which preference keys exist and what theme and density may hold.
public static class PreferenceKeys
{
    public const string ViewPrefix = "view:";

    public static void Require(string key)
    {
        var known = key is "theme" or "density" or "recentProjects"
            || (key.StartsWith(ViewPrefix, StringComparison.Ordinal) && key.Length > ViewPrefix.Length && key.Length <= 100 && !key.Any(char.IsWhiteSpace));
        if (!known)
        {
            throw ApiException.Validation("That preference does not exist.");
        }
    }
}
