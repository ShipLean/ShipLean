using ShipLean.Application.Features.Mcp.Planning.Waves;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Planning.Capture;

// A story with its parsed wave. Stories are created in wave order, so the backlog reads in wave order.
public sealed record PlannedStory(CapturePlanStory Story, WaveLabel Wave);
