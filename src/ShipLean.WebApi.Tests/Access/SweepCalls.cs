using System.Text.Json.Nodes;
using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Access;

// The shared parts of the sweeps: which rows each kind covers, the URL a row is called on, and the call itself.
public static class SweepCalls
{
    public static IEnumerable<TestCaseData> ProjectRows() => Cases(AccessSweepRows.All().Where(r => !r.InstanceScoped));

    public static IEnumerable<TestCaseData> AdminOnlyProjectRows()
        => Cases(AccessSweepRows.All().Where(r => !r.InstanceScoped && r.Minimum == ProjectRole.Admin));

    public static IEnumerable<TestCaseData> InstanceRows() => Cases(AccessSweepRows.All().Where(r => r.InstanceScoped));

    public static IEnumerable<TestCaseData> AllRows() => Cases(AccessSweepRows.All());

    public static string UrlOf(AccessSweepRow row) => "/" + (row.Url ?? ApiActions.Fill(row.RouteTemplate, SeedIds.Projects.Ship)).TrimStart('/');

    public static Task<HttpResponseMessage> SendAsync(ApiClient api, AccessSweepRow row)
    {
        var url = UrlOf(row);
        object? body = row.Body is null ? null : JsonNode.Parse(row.Body);
        return row.Method.ToUpperInvariant() switch
        {
            "GET" => api.GetAsync(url),
            "POST" => api.PostJsonAsync(url, body),
            "PUT" => api.PutJsonAsync(url, body),
            "PATCH" => api.PatchJsonAsync(url, body),
            "DELETE" => api.DeleteAsync(url),
            _ => throw new InvalidOperationException($"The sweep has no call for {row.Method}."),
        };
    }

    public static IEnumerable<TestCaseData> Cases(IEnumerable<AccessSweepRow> rows)
        => rows.Select(r => new TestCaseData(r).SetArgDisplayNames($"{r.Method} {r.RouteTemplate}"));
}
