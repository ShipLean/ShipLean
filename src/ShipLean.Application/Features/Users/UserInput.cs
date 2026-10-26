using ShipLean.Contracts.Errors;
using ShipLean.Core.People;

namespace ShipLean.Application.Features.Users;

// The rules for an email and a name typed by a person.
public static class UserInput
{
    public const int MaxEmail = 254;

    public static string RequireEmail(string? value)
    {
        var email = value?.Trim() ?? string.Empty;
        var at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1 || email.Any(char.IsWhiteSpace) || email.IndexOf('@', at + 1) >= 0)
        {
            throw ApiException.Validation("Enter an email address such as name@example.com.");
        }
        return email.Length > MaxEmail ? throw ApiException.Validation($"The email can have at most {MaxEmail} characters.") : email;
    }

    // The name when the name rules accept it, else null.
    public static string? UsableName(string? value)
        => DisplayName.Problem(value) is null ? value!.Trim() : null;

    // The part of an email before the @, for a user who arrives without a name.
    // Control and invisible formatting characters are dropped and the rest cut to the name limit, so it is always a valid name.
    public static string NameFromEmail(string cleanEmail)
    {
        var local = new string(cleanEmail[..cleanEmail.IndexOf('@')]
            .Where(c => !char.IsControl(c) && char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format).ToArray());
        local = local.Length > DisplayName.MaxLength ? local[..DisplayName.MaxLength] : local;
        return DisplayName.Problem(local) is null ? local.Trim() : "User";
    }

    public static string RequireName(string? value)
        => DisplayName.Problem(value) is { } problem ? throw ApiException.Validation(problem) : value!.Trim();
}
