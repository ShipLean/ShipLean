using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Index.Writer;

// A source text may carry each field once; the writer says which kind, source and field repeat.
[TestFixture]
public class SearchDocWriterDuplicateFieldTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;
    private static readonly Guid Repeating = Guid.NewGuid();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<ISearchTextSource>();
        services.AddSingleton<ISearchTextSource>(new RecordingTextSource(SearchSourceKind.Comment,
            new SearchSourceText(Repeating, Project, null, null, [new(SearchField.Comment, "One"), new(SearchField.Comment, "Two")])));
    };

    [Test]
    public async Task Index_WhenSourceTextRepeatsAField_ShouldThrowNamingKindSourceAndField()
    {
        using var scope = Host.Services.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<ISearchDocWriter>();

        var error = await Should.ThrowAsync<InvalidOperationException>(
            () => writer.SyncAsync([new SearchSource(SearchSourceKind.Comment, Repeating)]));

        error.Message.ShouldContain("Comment text source");
        error.Message.ShouldContain(Repeating.ToString());
        error.Message.ShouldContain("field Comment");
    }
}
