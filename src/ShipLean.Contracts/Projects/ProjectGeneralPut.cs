using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Projects;

// The General section as saved: the key is chosen at creation and never changes. The agent fields and the holiday country (ISO code, null = None) not named in Provided
// (default: all of them) keep their saved value; a provided null (or blank text) clears one. BugSeverityWeights null keeps the saved weights; otherwise it must hold
// exactly the severities 1 to 4, each a positive whole number.
public sealed record ProjectGeneralPut(
    string Name, string Description, IReadOnlyList<int> WorkingDays, Guid? AgentConnectionId = null, string? RepoUrl = null, string? RepoBaseBranch = null, string? HolidayCountry = null,
    [property: JsonIgnore] ProvidedAgentFields Provided = ProvidedAgentFields.All, IReadOnlyDictionary<int, int>? BugSeverityWeights = null);
