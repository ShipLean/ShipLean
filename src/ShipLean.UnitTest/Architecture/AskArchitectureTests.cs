using System.Reflection;
using ShipLean.Application;
using ShipLean.Contracts.Asks;
using ShipLean.WebApi;

namespace ShipLean.UnitTest.Architecture;

// D6, agents never wait: the remote-agent session path never records decisions or asks questions. Asks are written by the
// agent through the MCP tools and answered on the work item, where the next agent to pick it up reads them.
[TestFixture]
public class AskArchitectureTests
{
    private static readonly Type[] AskWrites = [typeof(IQuestionWrites), typeof(IDecisionRecorder)];

    [Test]
    public void AgentDispatch_ShouldNotDependOnQuestionWrites()
    {
        Assembly[] assemblies = [ApplicationMarker.Assembly, typeof(ApiMarker).Assembly];

        var dispatch = assemblies.SelectMany(a => a.GetTypes()).Where(t => FeatureScan.FeatureOf(t) == "AgentDispatch").ToList();
        var violations = dispatch
            .SelectMany(t => TypeReferences.Of(t).Where(AskWrites.Contains).Select(r => $"{t.FullName} -> {r.FullName}"))
            .ToList();

        foreach (var assembly in assemblies)
            assembly.GetTypes().Count(t => FeatureScan.FeatureOf(t) == "AgentDispatch").ShouldBeGreaterThan(0, assembly.GetName().Name);
        violations.ShouldBeEmpty();
    }
}
