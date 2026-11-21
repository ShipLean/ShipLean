using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Prompts;

namespace ShipLean.WebApi.Tests.Prompts;

// SHIP's guide v1, not in use: the versions/1/use sweep row names a version, and a made-up number is a 404 whether or not access is checked.
internal static class PromptSweepData
{
    public static Task InsertAsync(string connectionString)
        => new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Guide).WithNumber(1).InsertAsync(connectionString);
}
