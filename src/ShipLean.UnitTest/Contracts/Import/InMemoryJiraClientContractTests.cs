using ShipLean.Contracts.Import;
using ShipLean.TestSupport.Contracts.Import;

namespace ShipLean.UnitTest.Contracts.Import;

public sealed class InMemoryJiraClientContractTests : JiraClientContract
{
    protected override Task<JiraWorld> ArrangeAsync(JiraKind kind, JiraFault fault) => Task.FromResult(InMemoryJiraWorld.Create(kind, fault));
}
