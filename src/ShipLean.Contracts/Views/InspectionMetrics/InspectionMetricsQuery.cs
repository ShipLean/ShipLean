namespace ShipLean.Contracts.Views.InspectionMetrics;

// The query as it arrives, unparsed, so validation runs after the access check and the HTTP and MCP forms share it (K3).
// Range: last6 (default) | quarter | lastQuarter | all. Cut: none (default) | feature | who. Period: a sprint id of the project.
// Select: a task key, only with a period.
public sealed record InspectionMetricsQuery(string? Range = null, string? Cut = null, string? Period = null, string? Select = null);
