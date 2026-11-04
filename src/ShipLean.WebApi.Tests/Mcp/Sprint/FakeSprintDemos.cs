using ShipLean.Contracts.Demos;

namespace ShipLean.WebApi.Tests.Mcp.Sprint;

// ISprintDemos for the create_sprint_demo shape tests: a fixed handover, or the failure the test sets, and a record of what the tool passed.
public sealed class FakeSprintDemos : ISprintDemos
{
    public static readonly DateTimeOffset Expires = new(2031, 5, 14, 9, 30, 0, TimeSpan.Zero);

    private readonly List<DemoCall> _calls = [];

    public IReadOnlyList<DemoCall> Calls => _calls;

    public Exception? Failure { get; set; }

    public void Reset()
    {
        _calls.Clear();
        Failure = null;
    }

    public Task<SprintDemoHandover> CreateForAgentAsync(Guid sprintId, string baseUrl, string agentStamp, Guid? authorizationId, CancellationToken cancellationToken = default)
    {
        _calls.Add(new DemoCall(sprintId, baseUrl, agentStamp, authorizationId));
        if (Failure is not null)
        {
            throw Failure;
        }

        var brand = new DemoBrand(
            LogoIsDefault: false, LogoSvg: null, LogoUrl: "https://demo.example/api/downloads/demo-logo?token=abc", LogoContentType: "image/png",
            LogoUrlExpiresAt: Expires, TokensAreDefault: false, TokensCss: ":root { --brand: #123456; }", LogoFallbackReason: "The logo is a fallback.");
        return Task.FromResult(new SprintDemoHandover(
            sprintId, "SHIP-9001", "https://demo.example/p/SHIP/items/SHIP-9001", "https://demo.example/p/SHIP/sprints/x/demo",
            "https://demo.example/api/uploads/demo?token=up123", Expires, 157286400, brand, "# Make the demo\n\nStep 1."));
    }

    public Task<SprintDemoDto> GetAsync(Guid sprintId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SprintDemoDto> CreateFromButtonAsync(Guid sprintId, string requestOrigin, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task StartAutomaticAsync(Guid projectId, Guid sprintId, Guid closedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SprintDemoDto> RetryYouTubeAsync(Guid sprintId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
