namespace ShipLean.Contracts.Projects;

// Which of the project's optional General fields (the agent ones, and the holiday country) a settings write carried. A field not carried keeps its saved value; one carried
// as null or blank clears it.
[Flags]
public enum ProvidedAgentFields
{
    None = 0,
    Connection = 1,
    RepoUrl = 2,
    RepoBaseBranch = 4,
    HolidayCountry = 8,
    All = Connection | RepoUrl | RepoBaseBranch | HolidayCountry,
}
