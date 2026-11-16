using System.Data.Common;
using Agoda.IoC.Core;
using Dapper;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch.RunLogs;

// C10: the session's one-use agentLog link, issued at start on the starter's transaction, so the token exists only if the session does.
// The URL goes into the `created` guidance and nowhere else: it is never logged and no read returns it.
[RegisterSingleton]
public sealed class AgentRunLogLinks(ITransferTokens tokens)
{
    public const string ContentType = "text/plain";

    public const long MaxBytes = 1024 * 1024;

    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public static string NameOf(Guid sessionId) => $"agent-run-{sessionId}.log";

    // The upload URL, or null when neither the instance's base URL nor the request's origin is known: a relative link is no use to a relay.
    public async Task<string?> IssueAsync(
        DbTransaction transaction, Guid workItemId, Guid sessionId, Guid creatorId, string agentName, string? fallbackBaseUrl, CancellationToken cancellationToken)
    {
        var baseUrl = await transaction.Connection!.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT base_url FROM instance_settings WHERE btrim(base_url) <> ''", transaction: transaction, cancellationToken: cancellationToken));
        baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? fallbackBaseUrl : baseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        var issued = await tokens.IssueAsync(
            new NewTransfer(TransferKind.AgentLog, workItemId, null, NameOf(sessionId), ContentType, creatorId, null, agentName, Lifetime, sessionId),
            transaction,
            cancellationToken);
        return $"{baseUrl.TrimEnd('/')}/api/uploads/agent-log?token={Uri.EscapeDataString(issued.Token)}";
    }
}
