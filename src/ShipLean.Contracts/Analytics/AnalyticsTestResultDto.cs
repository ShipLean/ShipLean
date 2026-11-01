namespace ShipLean.Contracts.Analytics;

// POST /api/instance/analytics/test. Ok is false with the reason in Message when the test event could not be sent.
public sealed record AnalyticsTestResultDto(bool Ok, string Message);
