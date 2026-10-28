using HotChocolate.Configuration;
using HotChocolate.Types.Descriptors.Configurations;

namespace ShipLean.WebApi.Features.LinearApi;

// A schema-first union has no CLR types to tell its members apart, and the maps the mappers return are all dictionaries, so
// the union is told how: AgentActivityContent's members name themselves in __typename (LinearActivityMap).
internal sealed class LinearUnionResolution : TypeInterceptor
{
    public override void OnBeforeCompleteType(ITypeCompletionContext completionContext, TypeSystemConfiguration configuration)
    {
        if (configuration is UnionTypeConfiguration { Name: "AgentActivityContent" } union)
        {
            union.ResolveAbstractType = LinearFieldDefaults.ContentType;
        }
    }
}
