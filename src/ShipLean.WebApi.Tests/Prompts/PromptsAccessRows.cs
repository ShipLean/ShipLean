using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Prompts;

// The Prompts routes: reads need Member, writes Admin. The usage reports are Member. The versions/1/use row passes only against the
// stand-in writes, which accept any number; the real writes need a version 1 on SHIP, which the sweep data arranges.
public sealed class PromptsAccessRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{id:guid}/prompts", ProjectRole.Member),
        new("GET", "api/projects/{id:guid}/prompts/{prompt}/history", ProjectRole.Member, Url: $"api/projects/{SeedIds.Projects.Ship}/prompts/guide/history"),
        new("GET", "api/projects/{id:guid}/prompts/{prompt}/assembled", ProjectRole.Member, Url: $"api/projects/{SeedIds.Projects.Ship}/prompts/guide/assembled?agent=other"),
        new("POST", "api/projects/{id:guid}/prompts/{prompt}/versions", ProjectRole.Admin, Body: """{"text":"Custom text.","note":"A change.","source":"editor"}""", Url: $"api/projects/{SeedIds.Projects.Ship}/prompts/guide/versions"),
        new("POST", "api/projects/{id:guid}/prompts/{prompt}/use-built-in", ProjectRole.Admin, Url: $"api/projects/{SeedIds.Projects.Ship}/prompts/guide/use-built-in"),
        new("POST", "api/projects/{id:guid}/prompts/{prompt}/versions/{number:int}/use", ProjectRole.Admin, Url: $"api/projects/{SeedIds.Projects.Ship}/prompts/guide/versions/1/use"),
        new("POST", "api/projects/{id:guid}/prompts/{prompt}/update/keep", ProjectRole.Admin, Url: $"api/projects/{SeedIds.Projects.Ship}/prompts/guide/update/keep"),
        new("POST", "api/projects/{id:guid}/usage/prompts-opened", ProjectRole.Member),
        new("POST", "api/projects/{id:guid}/usage/prompt-customize-opened", ProjectRole.Member, Body: """{"prompt":"guide"}"""),
        new("POST", "api/projects/{id:guid}/usage/prompts-search-picked", ProjectRole.Member, Body: """{"prompt":"guide"}"""),
    ];
}
