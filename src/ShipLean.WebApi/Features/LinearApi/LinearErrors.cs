using HotChocolate;

namespace ShipLean.WebApi.Features.LinearApi;

// GraphQL errors in Linear's wording and with codes a client can switch on. A thing that exists but belongs to another
// connection is the same NOT_FOUND as one that does not exist.
internal static class LinearErrors
{
    public static GraphQLException NotFound(string entity)
        => new(ErrorBuilder.New().SetMessage($"Entity not found: {entity}").SetCode("NOT_FOUND").SetExtension("type", "invalid input").SetExtension("userError", true).Build());

    public static GraphQLException Invalid(string message)
        => new(ErrorBuilder.New().SetMessage(message).SetCode("INVALID_INPUT").SetExtension("type", "invalid input").SetExtension("userError", true).Build());
}
