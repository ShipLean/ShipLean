using System.Text.Json;
using HotChocolate.Resolvers;
using HotChocolate.Types;

namespace ShipLean.WebApi.Features.LinearApi;

// The resolution of every field whose parent is an object map (everything below a root field). A key the mapper set is
// the value (a LinearLazy is called); a key it did not set is null when the schema allows it and otherwise a default of the
// field's type: "" , 0, false, the epoch, [] , {} or an enum's first value. That is how the schema, built from every field the
// SDK selects, can answer fields ShipLean has nothing for without a resolver each. Root fields have no map parent and run their own resolvers.
public static class LinearFieldDefaults
{
    public static FieldMiddleware Middleware => next => async context =>
    {
        if (context.Parent<object?>() is not IReadOnlyDictionary<string, object?> parent)
        {
            await next(context);
            return;
        }

        var name = context.Selection.Field.Name;
        if (!parent.TryGetValue(name, out var value))
        {
            context.Result = AsJson(context.Selection.Field.Type, Default(context.Selection.Field.Type));
            return;
        }
        context.Result = AsJson(context.Selection.Field.Type, value is LinearLazy lazy ? await lazy(context) : value);
    };

    // The activity content union: every content map names its member in __typename.
    public static ObjectType? ContentType(IResolverContext context, object result)
        => result is IReadOnlyDictionary<string, object?> map && map.GetValueOrDefault("__typename") is string name
            ? context.Schema.Types.GetType<ObjectType>(name)
            : null;

    // The JSON-like scalars (JSON, JSONObject, TimelessDate, DateTimeOrDuration) are HotChocolate's AnyType, whose runtime value
    // is a JsonElement: maps, lists and primitives are converted on the way out.
    private static object? AsJson(IType type, object? value)
        => value is null or JsonElement || type.NamedType() is not AnyType ? value : JsonSerializer.SerializeToElement(value);

    private static object? Default(IType type)
    {
        if (type is not NonNullType nonNull)
        {
            return null;
        }

        return nonNull.NullableType switch
        {
            ListType => new List<object?>(),
            IEnumTypeDefinition @enum => @enum.Values[0].Name,
            AnyType any => any.Name == "JSONObject" ? new Dictionary<string, object?>() : string.Empty,
            ScalarType scalar => Scalar(scalar.Name),
            _ => new Dictionary<string, object?>(),
        };
    }

    private static object Scalar(string name) => name switch
    {
        "Int" => 0,
        "Float" => 0.0,
        "Boolean" => false,
        "DateTime" => DateTimeOffset.UnixEpoch,
                _ => string.Empty,
    };
}
