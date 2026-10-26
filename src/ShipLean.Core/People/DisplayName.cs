using System.Globalization;

namespace ShipLean.Core.People;

// The rule for a display name, for every way a name is typed (the owner's profile, an admin adding a user). Core cannot throw
// the API's errors, so it answers the message and each feature throws its own validation.
public static class DisplayName
{
    public const int MaxLength = 100;

    private const char ZeroWidthJoiner = '‍';

    // Why the name (trimmed first) is refused, or null when it is fine: 1 to 100 characters, no control characters and no
    // invisible formatting characters (zero-width space, bidi overrides and isolates) that let a name pass as another. The
    // zero-width joiner stays: emoji sequences need it.
    public static string? Problem(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return "Enter a name.";
        }

        if (name.Length > MaxLength)
        {
            return $"The name can have at most {MaxLength} characters.";
        }

        return name.Any(IsRefused) ? "The name cannot contain control or invisible formatting characters." : null;
    }

    private static bool IsRefused(char c)
        => char.IsControl(c) || (c != ZeroWidthJoiner && char.GetUnicodeCategory(c) == UnicodeCategory.Format);
}
