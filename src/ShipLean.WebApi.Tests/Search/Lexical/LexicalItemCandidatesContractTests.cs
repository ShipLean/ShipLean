using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.Search.Ranking;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Search;

namespace ShipLean.WebApi.Tests.Search.Lexical;

// ItemCandidatesContract against the real lexical leg. Strong = a title row and a body row, Weak = the title row only, both in the
// title list. The Strong title repeats the word and the Weak one buries it in filler, so ts_rank_cd puts Strong first in that list.
[TestFixture]
public class LexicalItemCandidatesContractTests : ItemCandidatesContract
{
    protected override CandidateLeg Leg => CandidateLeg.Lexical;

    protected override string QueryText => "lexicalneedle";

    protected override async Task ArrangeMatchAsync(NpgsqlConnection connection, Guid projectId, Guid workItemId, string itemTitle, MatchStrength strength)
    {
        var text = strength == MatchStrength.Strong ? $"{QueryText} {QueryText} {QueryText}" : $"{itemTitle} filler words that dilute the match here";
        await new SearchDocBuilder(projectId, SearchSourceKind.Item, workItemId, SearchField.Title, text).WithWorkItem(workItemId).InsertAsync(connection);
        if (strength == MatchStrength.Strong)
        {
            await new SearchDocBuilder(projectId, SearchSourceKind.Item, workItemId, SearchField.Body, $"the body says {QueryText} too").WithChunk(0).WithWorkItem(workItemId).InsertAsync(connection);
        }
    }

    [Test]
    public async Task Lists_WhenHostResolved_ShouldHaveOneLexicalLeg()
    {
        using var scope = Host.Services.CreateScope();

        scope.ServiceProvider.GetServices<IItemCandidates>().Count(c => c.Leg == CandidateLeg.Lexical).ShouldBe(1);
    }
}
