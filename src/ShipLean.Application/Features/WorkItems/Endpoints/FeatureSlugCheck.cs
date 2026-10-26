using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;

namespace ShipLean.Application.Features.WorkItems.Endpoints;

// The New Feature dialog's live slug check: the access check first, then the shared Slugs rule, then uniqueness per project
// (case-insensitive, the same rule as the unique index). A taken slug is simply unavailable; an invalid one says why.
[RegisterPerRequest]
public sealed class FeatureSlugCheck(IProjectAccess access, NpgsqlDataSource dataSource)
{
    public const string InvalidReason = "Use lower-case letters and digits in groups joined by single hyphens, at most 64 characters.";

    public async Task<SlugCheckDto> CheckAsync(Guid projectId, string? value, CancellationToken cancellationToken)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        var slug = value ?? "";
        if (!Slugs.IsValid(slug))
        {
            return new SlugCheckDto(slug, false, InvalidReason);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var taken = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM work_item WHERE project_id = @projectId AND lower(slug) = lower(@slug))",
                new { projectId, slug },
                cancellationToken: cancellationToken));
        return new SlugCheckDto(slug, !taken, null);
    }
}
