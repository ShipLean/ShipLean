using System.Text.Json.Serialization;
using ShipLean.Contracts.Projects;

namespace ShipLean.WebApi.Features.ProjectSettings;

// The General section of the body. The agent fields tell a field left out of the JSON (the saved value stays) from an
// explicit null or blank (it is cleared): the serializer only calls a setter for a field that is in the body.
[method: JsonConstructor]
public sealed record ProjectGeneralRequest(string? Key, string Name, string Description, IReadOnlyList<int> WorkingDays)
{
    private ProvidedAgentFields _provided = ProvidedAgentFields.None;

    public ProjectGeneralRequest(
        string? key, string name, string description, IReadOnlyList<int> workingDays, Guid? agentConnectionId, string? repoUrl = null, string? repoBaseBranch = null)
        : this(key, name, description, workingDays)
    {
        AgentConnectionId = agentConnectionId;
        RepoUrl = repoUrl;
        RepoBaseBranch = repoBaseBranch;
    }

    public Guid? AgentConnectionId
    {
        get;
        init
        {
            field = value;
            _provided |= ProvidedAgentFields.Connection;
        }
    }

    public string? RepoUrl
    {
        get;
        init
        {
            field = value;
            _provided |= ProvidedAgentFields.RepoUrl;
        }
    }

    public string? RepoBaseBranch
    {
        get;
        init
        {
            field = value;
            _provided |= ProvidedAgentFields.RepoBaseBranch;
        }
    }

    [JsonIgnore]
    public ProvidedAgentFields Provided => _provided;
}
