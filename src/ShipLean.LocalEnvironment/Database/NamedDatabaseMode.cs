namespace ShipLean.LocalEnvironment.Database;

public enum NamedDatabaseMode
{
    // Drop the database, then recreate it from today's seed template.
    Fresh,

    // Open it if it exists and apply pending migrations (no seed); otherwise Fresh.
    Keep,

    // Drop the database, then recreate it from the schema template: the schema and no rows.
    Empty,
}
