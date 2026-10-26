using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Search;

namespace ShipLean.WebApi.Tests.Search.Doubles;

// POST /api/admin/search/reindex against a recording indexer: the route passes full through and returns the pass counts.
[TestFixture]
public class SearchAdminReindexTests : ApiFixture
{
    private readonly RecordingSearchIndexer _indexer = new() { Result = new IndexPassResult(7, 5, 2) };

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.Replace(ServiceDescriptor.Singleton<ISearchIndexer>(_indexer));

    [Test]
    public async Task Reindex_WhenInstanceAdmin_ShouldReturnPassCounts()
    {
        var before = _indexer.Calls.Count;
        using var response = await Api.PostJsonAsync("api/admin/search/reindex");
        using var full = await Api.PostJsonAsync("api/admin/search/reindex?full=true");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<IndexPassResult>()).ShouldBe(new IndexPassResult(7, 5, 2));
        full.StatusCode.ShouldBe(HttpStatusCode.OK);
        _indexer.Calls.Skip(before).ShouldBe([false, true]);
    }

    [Test]
    public async Task Reindex_WhenNotInstanceAdmin_ShouldBeForbidden()
    {
        var before = _indexer.Calls.Count;
        using var response = await Api.ActingAs(TestUser.Mei).PostJsonAsync("api/admin/search/reindex");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        _indexer.Calls.Count.ShouldBe(before);
    }
}
