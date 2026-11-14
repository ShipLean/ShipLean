using System.Reflection;

namespace ShipLean.Contracts.Usage;

// The extra property keys an event may carry. The standard properties are not here: the dispatcher derives them from the record.
public static class UsageProps
{
    public const string Screen = "screen";
    public const string Board = "board";
    public const string Provider = "provider";
    public const string Count = "count";

    // A backlog column sort, on backlog.sorted: the column id and asc / desc / off.
    public const string Column = "column";
    public const string Direction = "direction";

    // The backlog mode (list, planning or tree) on screen.viewed for a backlog screen, so a mode's views compare with the page's.
    public const string Mode = "mode";

    // The SPA's width class (UsageViewports), on every event of a web request that sent a valid ViewportHeader.
    public const string Viewport = "viewport";

    // The client being connected, on agent.connected / agent.consent_denied; cut to 64 characters.
    public const string AgentName = "agent_name";

    // How a work item was moved (MoveMethods) on work_item.moved, or how the user signed in (SignInMethods) on auth.signed_in.
    public const string Method = "method";

    // What started a backup run, on backup.run_finished: "scheduled" or "manual".
    public const string Trigger = "trigger";

    // How a decision was answered, on decision.confirmed / decision.changed: recommended, alternative or own.
    public const string AnswerKind = "answer_kind";

    // How an opted-in MCP tool call got its arguments: "payload" (uploaded with create_payload_upload) or "inline".
    public const string Input = "input";

    // Why a device session ended, on device_session.ended: one of DeviceSessionEndReasons.
    public const string Reason = "reason";

    public static readonly IReadOnlySet<string> All = typeof(UsageProps)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);
}
