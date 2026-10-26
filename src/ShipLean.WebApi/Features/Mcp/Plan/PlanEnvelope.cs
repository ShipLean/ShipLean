using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Features.Mcp.Plan;

// Seals a preview into the opaque `plan` string the apply call hands back. It is Data Protection under its own purpose,
// so it cannot be forged or read, and it names the user and the project, so another user's plan (or another project's)
// is refused. A plan lives 15 minutes. The expiry is part of the sealed payload and is checked against TimeProvider
// (the time-limited protector reads the system clock, which a test cannot move, and reports expiry as a crypto error
// that cannot be told from tampering).
public sealed class PlanEnvelope(IDataProtectionProvider provider, ICurrentUser currentUser, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private const string Purpose = "ShipLean.Mcp.Plan.v1";

    // kind names the tool that seals and opens the plan ("plan_sprint"): a sub-purpose, so a plan sealed for one kind
    // does not open for another.
    public string Seal<T>(string kind, Guid projectId, T body)
        => Protector(kind).Protect(JsonSerializer.Serialize(new Sealed<T>(currentUser.Id, projectId, clock.GetUtcNow().Add(Lifetime), body)));

    public T Open<T>(string kind, string? plan, Guid projectId)
    {
        if (string.IsNullOrWhiteSpace(plan))
        {
            throw Unreadable();
        }

        Sealed<T> sealedPlan;
        try
        {
            sealedPlan = JsonSerializer.Deserialize<Sealed<T>>(Protector(kind).Unprotect(plan.Trim()))
                         ?? throw new JsonException("Empty plan.");
        }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException or ArgumentException)
        {
            throw Unreadable();
        }

        if (sealedPlan.UserId != currentUser.Id || sealedPlan.ProjectId != projectId)
        {
            throw Unreadable();
        }
        return clock.GetUtcNow() > sealedPlan.ExpiresAt
            ? throw ApiException.Conflict("The preview expired. Preview again, then apply it within 15 minutes.")
            : sealedPlan.Body;
    }

    private IDataProtector Protector(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        return provider.CreateProtector(Purpose, kind);
    }

    private static ApiException Unreadable() => ApiException.Validation("That plan is not valid for this project and user. Preview again and pass the plan it returns.", "plan");

    private sealed record Sealed<T>(Guid UserId, Guid ProjectId, DateTimeOffset ExpiresAt, T Body);
}
