namespace ShipLean.WebApi.Features.Mcp.Planning;

// The {{name}} placeholders of the two templates that carry any, each exactly once and in the order listed.
public static class PlanPlaceholders
{
    public const string CommonBrief = "{{common_brief}}";
    public const string TaskKey = "{{task_key}}";
    public const string TaskTitle = "{{task_title}}";
    public const string TaskBrief = "{{task_brief}}";
    public const string SharedBranch = "{{shared_branch}}";
    public const string ReportTemplate = "{{report_template}}";

    public const string FeatureKey = "{{feature_key}}";
    public const string StoryKey = "{{story_key}}";
    public const string Wave = "{{wave}}";
    public const string Lane = "{{lane}}";
    public const string Tasks = "{{tasks}}";
    public const string Contracts = "{{contracts}}";

    public static readonly IReadOnlyList<string> Brief = [CommonBrief, TaskKey, TaskTitle, TaskBrief, SharedBranch, ReportTemplate];
    public static readonly IReadOnlyList<string> Lead = [FeatureKey, StoryKey, Wave, Lane, Tasks, Contracts, SharedBranch];
}
