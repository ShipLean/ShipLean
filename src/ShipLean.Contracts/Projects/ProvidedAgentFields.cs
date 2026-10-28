namespace ShipLean.Contracts.Projects;

// Which of the project's agent fields a settings write carried. A field not carried keeps its saved value; one carried
// as null or blank clears it.
[Flags]
public enum ProvidedAgentFields
{
    None = 0,
    Connection = 1,
    RepoUrl = 2,
    RepoBaseBranch = 4,
    All = Connection | RepoUrl | RepoBaseBranch,
}
