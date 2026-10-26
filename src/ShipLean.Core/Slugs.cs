using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ShipLean.Core;

// A Feature's slug: lower-case letters and digits in groups joined by single hyphens, at most 64 characters. The
// server's check (IsValid) and the suggestion typed into the New Feature dialog and by create_feature (FromTitle) are
// the same rule, so a suggested slug is always acceptable.
public static partial class Slugs
{
    public const int MaxLength = 64;

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*\z")]
    private static partial Regex Pattern();

    public static bool IsValid(string? slug) => slug is { Length: > 0 and <= MaxLength } && Pattern().IsMatch(slug);

    // Accents are dropped (é becomes e); any other run of characters that aren't ASCII letters or digits becomes one hyphen.
    // Returns "" when the title has nothing to build a slug from. Cut at MaxLength without leaving a trailing hyphen.
    public static string FromTitle(string? title)
    {
        var slug = new StringBuilder();
        var pendingHyphen = false;
        foreach (var c in (title ?? "").Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (c is >= 'a' and <= 'z' or >= '0' and <= '9' or >= 'A' and <= 'Z')
            {
                if (pendingHyphen && slug.Length > 0)
                {
                    slug.Append('-');
                }

                pendingHyphen = false;
                slug.Append(char.ToLowerInvariant(c));
            }
            else
            {
                pendingHyphen = true;
            }
        }

        return slug.Length <= MaxLength ? slug.ToString() : slug.ToString(0, MaxLength).TrimEnd('-');
    }
}
