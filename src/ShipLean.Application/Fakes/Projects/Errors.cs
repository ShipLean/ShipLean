using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Fakes.Projects;

internal static class Errors
{
    public static ApiException NotFound(string what) => ApiException.NotFound($"There is no such {what}.");
}
