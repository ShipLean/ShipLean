using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Instance;

namespace ShipLean.Application.Features.Instance;

// The install's one instance_settings row, read and written per request (nothing is cached: the project creation gate
// and the agent sign-in read the same row).
[RegisterPerRequest]
public sealed class InstanceSettings(NpgsqlDataSource dataSource, TimeProvider clock)
{
    private sealed record SettingsRow(string Name, string BaseUrl, string ProjectCreation);

    public async Task<InstanceSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadAsync(connection, cancellationToken);
    }

    public async Task<InstanceSettingsDto> SaveAsync(InstanceSettingsDto settings, CancellationToken cancellationToken)
    {
        var name = (settings.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw ApiException.Validation("The instance name can't be blank.", "name");
        }
        var baseUrl = NormaliseBaseUrl(settings.BaseUrl);
        if (!Enum.IsDefined(settings.ProjectCreation))
        {
            throw ApiException.Validation("Project creation must be everyone or admins.", "projectCreation");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE instance_settings SET name = @name, base_url = @baseUrl, project_creation = @projectCreation, updated_at = @now",
            new { name, baseUrl, projectCreation = settings.ProjectCreation == ProjectCreation.Admins ? "admins" : "everyone", now = clock.GetUtcNow() },
            cancellationToken: cancellationToken));
        return await ReadAsync(connection, cancellationToken);
    }

    // An absolute http(s) address with no path beyond "/", query, fragment or credentials; returned without the trailing "/".
    private static string NormaliseBaseUrl(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.AbsolutePath != "/"
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || uri.UserInfo.Length > 0)
        {
            throw ApiException.Validation("The base URL must be an http or https address with no path, such as https://shiplean.example.com.", "baseUrl");
        }
        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static async Task<InstanceSettingsDto> ReadAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleAsync<SettingsRow>(new CommandDefinition(
            "SELECT name, base_url AS baseUrl, project_creation AS projectCreation FROM instance_settings", cancellationToken: cancellationToken));
        return new InstanceSettingsDto(row.Name, row.BaseUrl, row.ProjectCreation == "admins" ? ProjectCreation.Admins : ProjectCreation.Everyone);
    }
}
