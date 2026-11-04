using ShipLean.Contracts.Demos;

namespace ShipLean.WebApi.Tests.Mcp.Sprint;

// ISprintDemos for the create_sprint_demo shape tests: a fixed handover, or the failure the test sets, and a record of what the tool passed.
public sealed class FakeSprintDemos : ISprintDemos
{
    public static readonly DateTimeOffset Expires = new(2031, 5, 14, 9, 30, 0, TimeSpan.Zero);

    private readonly List<DemoCall> _calls = [];

    public IReadOnlyList<DemoCall> Calls => _calls;

    public Exception? Failure { get; set; }

    // A project's own logo and tokens: a link, no inline SVG, no fallback reason (K18).
    public static DemoBrand ProjectBrand { get; } = new(
        LogoIsDefault: false, LogoSvg: null, LogoUrl: "https://demo.example/api/downloads/demo-logo?token=abc", LogoContentType: "image/png",
        LogoUrlExpiresAt: Expires, TokensAreDefault: false, TokensCss: ":root { --brand: #123456; }", LogoFallbackReason: null);

    // ShipLean's logo standing in because the project's could not be prepared: inline SVG, no link, and the reason.
    public static DemoBrand FallbackBrand { get; } = new(
        LogoIsDefault: true, LogoSvg: "<svg/>", LogoUrl: null, LogoContentType: null,
        LogoUrlExpiresAt: null, TokensAreDefault: true, TokensCss: ":root { --brand: #654321; }", LogoFallbackReason: "The logo is a fallback.");

    public DemoBrand Brand { get; set; } = ProjectBrand;

    public void Reset()
    {
        _calls.Clear();
        Failure = null;
        Brand = ProjectBrand;
    }

    public Task<SprintDemoHandover> CreateForAgentAsync(Guid sprintId, string baseUrl, string agentStamp, Guid? authorizationId, CancellationToken cancellationToken = default)
    {
        _calls.Add(new DemoCall(sprintId, baseUrl, agentStamp, authorizationId));
        if (Failure is not null)
        {
            throw Failure;
        }

        return Task.FromResult(new SprintDemoHandover(
            sprintId, "SHIP-9001", "https://demo.example/p/SHIP/items/SHIP-9001", "https://demo.example/p/SHIP/sprints/x/demo",
            "https://demo.example/api/uploads/demo?token=up123", Expires, 157286400, Brand, "# Make the demo\n\nStep 1."));
    }

    public Task<SprintDemoDto> GetAsync(Guid sprintId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SprintDemoDto> CreateFromButtonAsync(Guid sprintId, string requestOrigin, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task StartAutomaticAsync(Guid projectId, Guid sprintId, Guid closedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<SprintDemoDto> RetryYouTubeAsync(Guid sprintId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
