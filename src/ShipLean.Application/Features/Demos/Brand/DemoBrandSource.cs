using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Brand;

// What a demo run's agent is given as the project's brand (K18). Two steps so the blob store is never touched while a transaction is open:
// InspectAsync reads the row and checks the logo's blob, holding no lock; PrepareAsync only issues the logo's download token on the caller's transaction.
public sealed class DemoBrandSource(NpgsqlDataSource dataSource, IBlobStore blobs, ITransferTokens tokens, ILogger<DemoBrandSource> logger) : IDemoBrandSource
{
    public const string LogoFallbackReason = "The project's logo couldn't be prepared, so ShipLean's is used.";

    private static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(24);

    public async Task<DemoBrandInspection> InspectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<(string? LogoBlob, string? Tokens)>(new CommandDefinition(
            "SELECT logo_blob AS LogoBlob, tokens AS Tokens FROM project_demo_settings WHERE project_id = @projectId",
            new { projectId }, cancellationToken: cancellationToken));
        var css = TokensCss(projectId, row.Tokens);
        if (row.LogoBlob is null)
        {
            return new DemoBrandInspection(null, null, css, null);
        }

        var type = await LogoTypeAsync(projectId, row.LogoBlob, cancellationToken);
        return type is null
            ? new DemoBrandInspection(null, null, css, LogoFallbackReason)
            : new DemoBrandInspection(row.LogoBlob, type, css, null);
    }

    public async Task<DemoBrand> PrepareAsync(
        DemoBrandInspection inspection,
        Guid projectId,
        Guid itemId,
        Guid userId,
        Guid? authorizationId,
        string agentStamp,
        string baseUrl,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var css = inspection.TokensCss ?? DemoBrandDefaults.TokensCss;
        var tokensAreDefault = css == DemoBrandDefaults.TokensCss;
        if (inspection.LogoBlob is null || inspection.LogoContentType is null)
        {
            return new DemoBrand(true, DemoBrandDefaults.LogoSvg, null, null, null, tokensAreDefault, css, inspection.LogoFallbackReason);
        }

        var issued = await tokens.IssueAsync(
            new NewTransfer(TransferKind.DemoLogo, itemId, null, "logo", inspection.LogoContentType, userId, authorizationId, agentStamp, LinkLifetime),
            transaction,
            cancellationToken);
        var url = $"{baseUrl.TrimEnd('/')}/api/downloads/demo-logo?token={Uri.EscapeDataString(issued.Token)}";
        return new DemoBrand(false, null, url, inspection.LogoContentType, issued.ExpiresAt, tokensAreDefault, css, null);
    }

    // The stored type of the logo's blob, or null when the blob store throws, the blob is gone, or it isn't a type a logo can have. Never throws for those.
    private async Task<string?> LogoTypeAsync(Guid projectId, string blob, CancellationToken cancellationToken)
    {
        try
        {
            if (!await blobs.ExistsAsync(blob, cancellationToken))
            {
                logger.LogWarning("Project {ProjectId}'s demo logo blob {Blob} is gone; ShipLean's logo is used.", projectId, blob);
                return null;
            }

            var content = await blobs.GetAsync(blob, cancellationToken);
            if (content is null)
            {
                logger.LogWarning("Project {ProjectId}'s demo logo blob {Blob} is gone; ShipLean's logo is used.", projectId, blob);
                return null;
            }

            await using (content.Content)
            {
                if (DemoLogoFile.ServableTypes.Contains(content.ContentType))
                {
                    return content.ContentType;
                }
            }

            logger.LogWarning("Project {ProjectId}'s demo logo blob {Blob} has a type a logo can't have; ShipLean's logo is used.", projectId, blob);
            return null;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogWarning(failure, "Project {ProjectId}'s demo logo blob {Blob} couldn't be read; ShipLean's logo is used.", projectId, blob);
            return null;
        }
    }

    // ShipLean's tokens.css, then, when the project set any of the seven names, a :root rule holding only those lines.
    private string TokensCss(Guid projectId, string? stored)
    {
        var parsed = DemoTokens.Parse(stored);
        if (parsed.IsRefused)
        {
            logger.LogWarning("Project {ProjectId}'s stored demo tokens are refused by the rules ({Refusal}); ShipLean's are used.", projectId, parsed.Refusal);
            return DemoBrandDefaults.TokensCss;
        }

        var used = parsed.Lines
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => DemoTokens.UsedNames.Contains(line[..line.IndexOf(':')]))
            .ToList();
        return used.Count == 0
            ? DemoBrandDefaults.TokensCss
            : $"{DemoBrandDefaults.TokensCss.TrimEnd()}\n:root {{\n{string.Join('\n', used.Select(line => $"  {line}"))}\n}}\n";
    }
}
