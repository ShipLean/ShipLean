using System.Text.Json;
using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Preferences;

namespace ShipLean.Application.Features.Shell;

// The current user's preferences: opaque JSON documents keyed by name. Nothing is deleted; reset nulls the view keys.
[RegisterPerRequest]
public sealed class Preferences(NpgsqlDataSource dataSource, ICurrentUser currentUser)
{
    // Null for no row or JSON null (the default applies).
    public async Task<UserPreferenceDto?> GetAsync(string key, CancellationToken cancellationToken)
    {
        PreferenceKeys.Require(key);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var json = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT json::text FROM user_preference WHERE user_id = @user AND key = @key", new { user = currentUser.Id, key }, cancellationToken: cancellationToken));
        if (json is null)
        {
            return null;
        }
        using var document = JsonDocument.Parse(json);
        var element = document.RootElement;
        return element.ValueKind == JsonValueKind.Null ? null : new UserPreferenceDto(key, element.Clone());
    }

    public async Task PutAsync(string key, JsonElement value, CancellationToken cancellationToken)
    {
        PreferenceKeys.Require(key);
        Validate(key, value);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO user_preference (user_id, key, json) VALUES (@user, @key, @json::jsonb)
            ON CONFLICT (user_id, key) DO UPDATE SET json = EXCLUDED.json, updated_at = now()
            """,
            new { user = currentUser.Id, key, json = value.GetRawText() }, cancellationToken: cancellationToken));
    }

    public async Task ResetViewsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE user_preference SET json = 'null'::jsonb, updated_at = now() WHERE user_id = @user AND key LIKE 'view:%'",
            new { user = currentUser.Id }, cancellationToken: cancellationToken));
    }

    private static void Validate(string key, JsonElement value)
    {
        var allowed = key switch
        {
            "theme" => value.ValueKind == JsonValueKind.String && value.GetString() is "os" or "light" or "dark",
            "density" => value.ValueKind == JsonValueKind.String && value.GetString() is "compact" or "comfortable",
            "recentProjects" => value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(v => v.ValueKind == JsonValueKind.String && Guid.TryParse(v.GetString(), out _)),
            _ => true,
        };
        if (!allowed)
        {
            throw ApiException.Validation($"That is not a valid value for {key}.");
        }
    }
}
