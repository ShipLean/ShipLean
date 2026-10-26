namespace ShipLean.Contracts;

// What a user may do in a project, lowest to highest. Member works with the project's content; Admin also changes
// its settings and who belongs to it.
public enum ProjectRole
{
    Member = 1,
    Admin = 2,
}
