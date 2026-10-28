using HotChocolate.Types;
using Microsoft.AspNetCore.Authentication;
using ShipLean.Contracts;
using ShipLean.WebApi.Features.LinearApi.Schema;

namespace ShipLean.WebApi.Features.LinearApi;

// The Linear-compatible GraphQL server (SHIP-186): the token scheme and its policy, and a schema-first HotChocolate server
// named "linear". The SDL is LinearSchemaSdl (generated from the pinned @linear/sdk documents); Query and Mutation fields run
// LinearQueries and LinearMutations, every field below them is read from the map its parent returned (LinearFieldDefaults).
// To add a mutation, fill in its method in LinearMutations: the schema already has the field and its payload type.
public sealed class LinearApiServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, LinearTokenHandler>(LinearApiPolicy.Scheme, _ => { });
        services.AddAuthorizationBuilder()
            .AddPolicy(LinearApiPolicy.Name, policy => policy.AddAuthenticationSchemes(LinearApiPolicy.Scheme).RequireAuthenticatedUser());

        services.AddGraphQLServer(LinearApiPolicy.SchemaName)
            .AddDocumentFromString(LinearSchemaSdl.Text)
            .AddType(new AnyType("JSON"))
            .AddType(new AnyType("JSONObject"))
            .AddType(new AnyType("TimelessDate"))
            .AddType(new AnyType("DateTimeOrDuration"))
            .AddResolver("Query", "viewer", LinearQueries.Viewer)
            .AddResolver("Query", "issue", LinearQueries.Issue)
            .AddResolver("Query", "agentSession", LinearQueries.AgentSession)
            .AddResolver("Query", "agentActivity", LinearQueries.AgentActivity)
            .AddResolver("Query", "issueRepositorySuggestions", LinearQueries.IssueRepositorySuggestions)
            .AddResolver("Mutation", "agentActivityCreate", LinearMutations.AgentActivityCreate)
            .AddResolver("Mutation", "agentSessionUpdate", LinearMutations.AgentSessionUpdate)
            .AddResolver("Mutation", "agentSessionUpdateExternalUrl", LinearMutations.AgentSessionUpdateExternalUrl)
            .AddResolver("Mutation", "commentCreate", LinearMutations.CommentCreate)
            .TryAddTypeInterceptor<LinearUnionResolution>()
            .UseField(LinearFieldDefaults.Middleware)
            // The SDK's own issue and session documents select hundreds of fields (about 1400 by HotChocolate's cost count and 6
            // levels deep; the default limit of 1000 would refuse them). The limits are raised to leave room above that and still refuse a document
            // built to be huge; the depth rule bounds nesting through the Issue -> Comment -> Issue cycles the schema has.
            .ModifyCostOptions(options => options.MaxFieldCost = MaxFieldCost)
            .AddMaxExecutionDepthRule(MaxExecutionDepth);
    }

    private const int MaxFieldCost = 5000;
    private const int MaxExecutionDepth = 15;
}
