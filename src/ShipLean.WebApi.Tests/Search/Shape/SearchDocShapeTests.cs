using Dapper;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Search.Shape;

// The search_doc contract (docs/search.md): the generated tsv, its weights and the unique key.
[TestFixture]
public class SearchDocShapeTests
{
    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await FreshDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [TestCase(SearchField.Title, "'checkout':1A")]
    [TestCase(SearchField.Project, "'checkout':1A")]
    [TestCase(SearchField.Tags, "'checkout':1B")]
    [TestCase(SearchField.Body, "'checkout':1C")]
    [TestCase(SearchField.Attachment, "'checkout':1C")]
    [TestCase(SearchField.SprintGoal, "'checkout':1C")]
    [TestCase(SearchField.Comment, "'checkout':1")]
    [TestCase(SearchField.SprintName, "'checkout':1")]
    public async Task SearchDocShape_WhenRowWritten_ShouldGenerateWeightedTsv(SearchField field, string expected)
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Checkout flow").InsertAsync(connection);
        var id = await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Item, Guid.NewGuid(), field, "Checkout").WithWorkItem(item).InsertAsync(connection);

        var tsv = await connection.QuerySingleAsync<string>("SELECT tsv::text FROM search_doc WHERE id = @id", new { id });

        // Weight D (comment, sprint name) is the default and is not printed, so it is the one with no A, B or C.
        var weight = expected[^1];
        tsv.ShouldContain(expected);
        if (!char.IsLetter(weight))
        {
            tsv.ShouldNotContain("1A");
            tsv.ShouldNotContain("1B");
            tsv.ShouldNotContain("1C");
        }
    }

    [Test]
    public async Task SearchDocShape_WhenKeyLikeText_ShouldMatchWithSimpleConfig()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Key text").InsertAsync(connection);
        var id = await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Item, item, SearchField.Body, "See SHIP-12 and parseOrderLine()").WithWorkItem(item).InsertAsync(connection);

        var matches = await connection.QuerySingleAsync<int>(
            "SELECT count(*)::int FROM search_doc WHERE id = @id AND tsv @@ websearch_to_tsquery('simple', @query)", new { id, query = "ship-12" });
        var identifier = await connection.QuerySingleAsync<int>(
            "SELECT count(*)::int FROM search_doc WHERE id = @id AND tsv @@ websearch_to_tsquery('simple', @query)", new { id, query = "parseOrderLine" });

        matches.ShouldBe(1);
        identifier.ShouldBe(1);
    }

    [Test]
    public async Task SearchDocShape_WhenBodyTitleLineHasWord_ShouldNotMatchItViaTsv()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Titleword page").InsertAsync(connection);
        var id = await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Item, item, SearchField.Body, "Titleword page\nsecondline content").WithWorkItem(item).InsertAsync(connection);

        var inTitleLine = await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM search_doc WHERE id = @id AND tsv @@ websearch_to_tsquery('english', 'titleword')", new { id });
        var inSecondLine = await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM search_doc WHERE id = @id AND tsv @@ websearch_to_tsquery('english', 'secondline')", new { id });

        inTitleLine.ShouldBe(0);
        inSecondLine.ShouldBe(1);
    }

    [Test]
    public async Task SearchDocShape_WhenEmbeddingWritten_ShouldRoundTripAsFloats()
    {
        await using var connection = await _database.OpenAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Vector text").InsertAsync(connection);
        var id = await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Item, item, SearchField.Title, "Vector text").WithWorkItem(item)
            .WithEmbedding([0.25f, -0.5f, 0.75f], new EmbeddingModelId("test-model", "1")).InsertAsync(connection);

        var row = await connection.QuerySingleAsync<(float[] Embedding, string Model)>("SELECT embedding, embedding_model FROM search_doc WHERE id = @id", new { id });

        row.Embedding.ShouldBe([0.25f, -0.5f, 0.75f]);
        row.Model.ShouldBe("test-model@1");
    }

    [Test]
    public async Task SearchDocShape_WhenSameKeyWrittenTwice_ShouldBeRejected()
    {
        await using var connection = await _database.OpenAsync();
        var source = Guid.NewGuid();
        await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Project, source, SearchField.Project, "Ship").InsertAsync(connection);

        var again = async () => await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Project, source, SearchField.Project, "Ship again").InsertAsync(connection);

        await again.ShouldThrowAsync<Npgsql.PostgresException>();
    }
}
