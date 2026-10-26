using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.AccessSweep;
using ShipLean.WebApi.Features.Auth;

namespace ShipLean.WebApi.Tests.Access;

public static partial class ApiActions
{
    private const string Fallback = "api/{**path}";

    // Every endpoint mapped under /api by the real app (controller actions and health), one per method, except the
    // catch-all that answers unknown routes with not_found. Test-only controllers (AuthTestController and the like) are
    // never here: they exist only in hosts that add this assembly as an application part, and these tests don't.
    public static IReadOnlyList<ApiAction> Of(IServiceProvider services)
        => services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e => (Endpoint: e, Template: Normalise(e.RoutePattern.RawText)))
            .Where(x => x.Template.StartsWith("api/", StringComparison.Ordinal) && x.Template != Fallback)
            .SelectMany(x => MethodsOf(x.Endpoint).Select(m => new ApiAction(
                m,
                x.Template,
                x.Endpoint.Metadata.GetMetadata<InstanceAdminAttribute>() is not null)))
            .OrderBy(a => a.Template, StringComparer.Ordinal).ThenBy(a => a.Method, StringComparer.Ordinal)
            .ToList();

    public static string Normalise(string? template) => (template ?? string.Empty).TrimStart('/').ToLowerInvariant();

    // The rows of the sweep that match no mapped route (method and template): stale rows.
    public static IReadOnlyList<AccessSweepRow> Unmatched(IEnumerable<AccessSweepRow> rows, IReadOnlyList<ApiAction> actions)
        => rows.Where(r => !actions.Any(a => Matches(a, r))).ToList();

    public static bool Matches(ApiAction action, AccessSweepRow row)
        => string.Equals(action.Method, row.Method, StringComparison.OrdinalIgnoreCase) && action.Template == Normalise(row.RouteTemplate);

    // The template with every {…} filled: the given id, or a throwaway value for catch-alls.
    public static string Fill(string template, Guid id) => System.Text.RegularExpressions.Regex.Replace(template, "{[^}]*}", id.ToString());

    // The http methods the endpoint declares; none declared is ApiAction.NoVerb, never an assumed GET.
    public static IEnumerable<string> MethodsOf(Endpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
        return methods is { Count: > 0 } ? methods : [ApiAction.NoVerb];
    }

    // The routes no row, allow-list entry or instance row accounts for. An [InstanceAdmin] route needs an instance row
    // (InstanceAdminWithoutRow) and is covered by it, except under api/projects/, where it is still a project route and
    // needs a project row too.
    public static IReadOnlyList<ApiAction> Uncovered(
        IEnumerable<ApiAction> actions, IReadOnlyList<AccessSweepRow> rows, Func<ApiAction, bool> allowListed)
        => actions
            .Where(a => !(a.InstanceAdmin && !IsProjectRoute(a)))
            .Where(a => !rows.Any(r => !r.InstanceScoped && Matches(a, r)))
            .Where(a => !allowListed(a))
            .ToList();

    // Every [InstanceAdmin] route needs an instance-scoped row, so it is called as a non-admin (403) and as an admin.
    public static IReadOnlyList<ApiAction> InstanceAdminWithoutRow(IEnumerable<ApiAction> actions, IReadOnlyList<AccessSweepRow> rows)
        => actions.Where(a => a.InstanceAdmin && !rows.Any(r => r.InstanceScoped && Matches(a, r))).ToList();

    // Rows whose template has a route parameter other than the leading project id and that give no Url, or a Url that is
    // still a template: the sweep would call a throwaway id there, and a 404 would then say nothing about access.
    public static IReadOnlyList<AccessSweepRow> RowsNeedingUrl(IEnumerable<AccessSweepRow> rows)
        => rows.Where(r => r.Url is null ? HasNonProjectParameter(r.RouteTemplate) : r.Url.Contains('{', StringComparison.Ordinal)).ToList();

    private static bool HasNonProjectParameter(string template)
        => LeadingProjectId().Replace(Normalise(template), string.Empty).Contains('{', StringComparison.Ordinal);

    [System.Text.RegularExpressions.GeneratedRegex(@"^api/projects/\{\w+:guid\}")]
    private static partial System.Text.RegularExpressions.Regex LeadingProjectId();

    public static bool IsProjectRoute(ApiAction action) => action.Template.StartsWith("api/projects/", StringComparison.Ordinal);
}
