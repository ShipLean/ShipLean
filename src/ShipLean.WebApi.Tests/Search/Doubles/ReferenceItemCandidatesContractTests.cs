using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ShipLean.Application.Features.Search.Ranking;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Search;

namespace ShipLean.WebApi.Tests.Search.Doubles;

// ItemCandidatesContract run against the reference leg, so the contract's plumbing (host, clone, acting user, removed state,
// ordering) is proven before the real legs use it. Strong = a title row and a body row, Weak = the title row only.
[TestFixture]
public class ReferenceItemCandidatesContractTests : ItemCandidatesContract
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IItemCandidates>();
        services.AddScoped<IItemCandidates, ReferenceItemCandidates>();
    };

    protected override CandidateLeg Leg => CandidateLeg.Lexical;

    protected override string QueryText => "reference needle";

    protected override async Task ArrangeMatchAsync(NpgsqlConnection connection, Guid projectId, Guid workItemId, string itemTitle, MatchStrength strength)
    {
        await new SearchDocBuilder(projectId, SearchSourceKind.Item, workItemId, SearchField.Title, itemTitle).WithWorkItem(workItemId).InsertAsync(connection);
        if (strength == MatchStrength.Strong)
        {
            await new SearchDocBuilder(projectId, SearchSourceKind.Item, workItemId, SearchField.Body, $"the body says {QueryText} too").WithChunk(0).WithWorkItem(workItemId).InsertAsync(connection);
        }
    }
}
