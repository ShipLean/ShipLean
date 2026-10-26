using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.Search.Ranking;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Search;

namespace ShipLean.WebApi.Tests.Search.Semantic;

// ItemCandidatesContract against the semantic leg on the host's FakeEmbedder. Strong embeds the query text itself (cosine 1) and Weak
// the item's longer title (about 0.8, above the cut-off); both are title rows, so the title list holds both.
[TestFixture]
public class SemanticItemCandidatesContractTests : ItemCandidatesContract
{
    protected override CandidateLeg Leg => CandidateLeg.Semantic;

    protected override string QueryText => "semantic needle";

    protected override async Task<float[]?> QueryVectorAsync() => await Embedder.EmbedQueryAsync(QueryText);

    protected override async Task ArrangeMatchAsync(NpgsqlConnection connection, Guid projectId, Guid workItemId, string itemTitle, MatchStrength strength)
    {
        var vector = (await Embedder.EmbedPassagesAsync([strength == MatchStrength.Strong ? QueryText : itemTitle]))[0];
        await new SearchDocBuilder(projectId, SearchSourceKind.Item, workItemId, SearchField.Title, itemTitle)
            .WithWorkItem(workItemId)
            .WithEmbedding(vector, Embedder.ModelId!)
            .InsertAsync(connection);
    }

    private IEmbedder Embedder => Host.Services.GetRequiredService<IEmbedder>();
}
