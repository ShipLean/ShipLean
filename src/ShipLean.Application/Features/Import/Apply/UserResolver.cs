using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Features.Import.Apply;

// The ShipLean user a Jira user stands for under the run's mapping. A person to create is made once, without a password. Anyone the
// mapping does not cover, and anyone it sends to the Jira import user, is attributed to that seeded user.
public sealed class UserResolver(IUserDirectory users)
{
    public async Task<Guid> ResolveAsync(ImportRun run, JiraUser? jira, CancellationToken cancellationToken)
    {
        if (jira is null)
        {
            return JiraImportUser.Id;
        }

        if (run.Users.TryGetValue(jira.AccountId, out var known))
        {
            return known;
        }

        var row = run.Mapping.Users.FirstOrDefault(u => u.JiraAccountId == jira.AccountId);
        var id = row switch
        {
            { Resolution: UserResolution.Matched or UserResolution.Existing, User: { } user } => user.Id,
            { Resolution: UserResolution.Create, Email: { Length: > 0 } email } => await CreateAsync(email, row.Name, cancellationToken),
            _ => JiraImportUser.Id,
        };
        run.Users[jira.AccountId] = id;
        return id;
    }

    private async Task<Guid> CreateAsync(string email, string name, CancellationToken cancellationToken)
    {
        try
        {
            return (await users.CreateAsync(email, name, cancellationToken)).Id;
        }
        catch (ApiException taken) when (taken.Code == ApiErrorCodes.Conflict)
        {
            var existing = await users.FindByEmailAsync(email, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            return existing.Id;
        }
    }
}
