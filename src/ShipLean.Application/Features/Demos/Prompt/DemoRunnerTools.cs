namespace ShipLean.Application.Features.Demos.Prompt;

// C2 mirror: the Bash command names a relay demo run may execute, in the order of agent-relay's ClaudeRunOptions.DEMO_ALLOWED_TOOLS
// (Bash(demo-python:*), Bash(ffmpeg:*), ...). The session-channel prompt uses no other command. Pinned on each side by an exact-string test.
public static class DemoRunnerTools
{
    public static readonly IReadOnlyList<string> Commands = ["demo-python", "ffmpeg", "ffprobe", "curl", "mkdir", "ls"];
}
