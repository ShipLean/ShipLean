using ShipLean.Contracts.Inspection;

namespace ShipLean.TestSupport.Inspection;

// One fixture pair: the OTLP/JSON a collector receives, and the RawRun a store must return for it. CompareKeys are the attribute
// keys RawRunComparer checks (lossy store names are left out).
public sealed record InspectionFixture(string Name, string OtlpPath, string AssembledPath, IReadOnlyList<string> CompareKeys, RawRun Expected);
