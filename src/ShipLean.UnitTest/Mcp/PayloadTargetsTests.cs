using System.Reflection;
using ModelContextProtocol.Server;
using ShipLean.WebApi.Features.Mcp.Payloads;

namespace ShipLean.UnitTest.Mcp;

// K5: a tool takes a payload only when its method says so with [McpPayload], always through the same nullable argument,
// and a payload fills only the tool's string arguments.
[TestFixture]
public class PayloadTargetsTests
{
    private const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Assembly WebApi = typeof(McpPayloadTargets).Assembly;

    [Test]
    public void PayloadTargets_WhenToolTakesPayload_ShouldListItsTextFields()
    {
        var tool = ToolNamed("capture_plan");

        var target = McpPayloadTargets.For(tool).ShouldNotBeNull();

        target.Tool.ShouldBe("capture_plan");
        target.TextFields.ShouldContain("common_brief");
        target.TextFields.ShouldContain("plan_markdown");
        target.TextFields.ShouldContain("shared_branch");
        target.TextFields.ShouldNotContain("plan");
        target.TextFields.ShouldNotContain("project");
        target.TextFields.ShouldNotContain("payload");
    }

    [Test]
    public void PayloadTargets_WhenToolTakesNoPayload_ShouldReturnNull()
    {
        McpPayloadTargets.For(ToolNamed("attach_file")).ShouldBeNull();
    }

    [Test]
    public void PayloadTargets_WhenMethodTakesPayload_ShouldBeMarkedAndNullable()
    {
        var nullability = new NullabilityInfoContext();
        var problems = new List<string>();
        foreach (var method in WebApi.GetTypes().SelectMany(type => type.GetMethods(Methods)))
        {
            var name = $"{method.DeclaringType!.FullName}.{method.Name}";
            var marked = method.GetCustomAttribute<McpPayloadAttribute>() is not null;
            var isTool = method.GetCustomAttribute<McpServerToolAttribute>() is not null;
            var parameters = method.GetParameters();
            var payload = parameters.SingleOrDefault(p => p.Name == McpPayloadTargets.ArgumentName);
            if (marked && !isTool)
            {
                problems.Add($"{name}: [McpPayload] without [McpServerTool]");
            }
            if (isTool && !marked && payload is not null)
            {
                problems.Add($"{name}: a payload argument without [McpPayload]");
            }
            if (!marked)
            {
                continue;
            }
            if (payload is null)
            {
                problems.Add($"{name}: [McpPayload] without a payload argument");
                continue;
            }
            var expectedPosition = parameters[^1].ParameterType == typeof(CancellationToken) ? parameters.Length - 2 : parameters.Length - 1;
            if (payload.Position != expectedPosition)
            {
                problems.Add($"{name}: payload is not the last argument before the CancellationToken");
            }
            if (payload.ParameterType != typeof(string) || nullability.Create(payload).WriteState != NullabilityState.Nullable
                || !payload.HasDefaultValue || payload.DefaultValue is not null)
            {
                problems.Add($"{name}: payload is not string? payload = null");
            }
        }

        problems.ShouldBeEmpty();
    }

    // The tool as the SDK builds it from its method (metadata from the method's attributes, schema from its parameters).
    private static McpServerTool ToolNamed(string name)
    {
        var method = WebApi.GetTypes()
            .SelectMany(type => type.GetMethods(Methods))
            .Single(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name == name);
        return McpServerTool.Create(method, _ => throw new InvalidOperationException("The tool is only listed, never called."));
    }
}
