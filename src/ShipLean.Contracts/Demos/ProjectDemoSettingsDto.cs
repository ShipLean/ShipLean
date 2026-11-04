namespace ShipLean.Contracts.Demos;

public sealed record ProjectDemoSettingsDto(bool Automatic, string Environment, DemoAgentCheckDto AgentCheck, ProjectDemoBrandDto Brand, YouTubeProjectDto YouTube);
