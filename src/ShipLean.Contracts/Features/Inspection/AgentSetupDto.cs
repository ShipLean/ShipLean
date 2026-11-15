namespace ShipLean.Contracts.Inspection;

// GET api/inspection/projects/{project}/agent-setup?agent=&item=.
public sealed record AgentSetupDto(string Agent, string AgentLabel, IReadOnlyList<AgentSetupFileDto> Files, IReadOnlyList<string> Notes, string DocsUrl);
