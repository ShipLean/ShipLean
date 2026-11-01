namespace ShipLean.LocalEnvironment.Database;

// A database with a name the caller chose (see ShipLeanDatabase.IsValidName). Pooled, Timezone=UTC, and never dropped on
// dispose: it outlives the process until DropNamedAsync or the next Fresh or Empty open.
public sealed record NamedDatabase(string Name, string ConnectionString);
