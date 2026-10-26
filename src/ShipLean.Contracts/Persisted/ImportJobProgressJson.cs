namespace ShipLean.Contracts.Persisted;

public sealed record ImportJobProgressJson(int Percent, ImportStep? Step, string Message, bool Rerun, ImportCounts Counts, int UsersMatched, int UsersCreated, int StatesCreated, ImportRerunResult? RerunResult);
