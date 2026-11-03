namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

// The learning-from-failure text both retrospective tools open with, and the instructions the logging tool adds.
internal static class RetroDescriptions
{
    public const string Preamble =
        "Your primary goal is absolute truth over artificial success. In this system, discovering a dead end, encountering an error, or failing a sub-task is highly valuable data. Do not fear failure; embrace it as diagnostic information. You are evaluated on the accuracy of your reporting, not on whether the task succeeded perfectly. "
        + "If you fail at a task, do not apologize, make excuses, or minimize the error. Adopt a clinical, blameless tone. Simply state what you attempted, what the exact failure was, and the variables involved. Treat errors as neutral engineering data. "
        + "Never fabricate or assume results to compensate for a failure. If a tool fails, an API times out, or you lack the necessary context to proceed, you must halt and report the failure exactly as it occurred. Faking a successful outcome is the only true failure.";

    public const string RawFailureLog =
        "When concluding your task, you must output a raw failure log. Include: Blockers: What specific constraints, missing data, or tool failures stopped you? "
        + "Assumptions Disproved: Did you try a strategy that turned out to be wrong? Why? "
        + "Friction: Which steps took longer or required more retries than expected? "
        + "Pass this directly to the Conductor without summarizing away the rough edges.";

    public const string BlamelessReviewer =
        "When compiling the Retrospective (What went well, What went bad, What can be improved), you must act as a blameless site reliability engineer. Do not soften the failures reported by the subagents. "
        + "Extract the root causes of their failures (for example, Agent A lacked sufficient search permissions, or Agent B's logic loop failed due to missing schema) to populate the went_badly and improve sections. "
        + "Include also things that came up that were not thought about in the plan, under not_in_plan.";

    public const string LogRetrospective =
        Preamble + " Log the retrospective of a work item you finished: what went well, what went badly, what can be improved, and what came up that the plan did not foresee. "
        + RawFailureLog + " Put that log in failure_log. " + BlamelessReviewer
        + " Each point is text plus an optional theme; went_badly points also take root_cause. Set partial when you are logging before the work is done. Pass compiled_from (item keys) when you compile the retrospective of other items. Logging an item again adds more points.";

    public const string ListRetrospectives =
        Preamble + " List the retrospective points logged in a project, newest first, with each point's theme, item, role and agent name, plus the theme mode and the project's theme list. Each point carries its decision (undecided, actioned or notNeeded; null on went well), the action item it became (key and state) and the reason it was not needed. Filter by sprint, feature, category, theme or date, by undecided (points still to decide), or by no_sprint (points with no sprint). Read the earlier points before you log yours, so you reuse a theme instead of inventing a new one.";
}
