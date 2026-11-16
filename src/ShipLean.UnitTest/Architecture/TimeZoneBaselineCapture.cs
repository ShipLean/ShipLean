namespace ShipLean.UnitTest.Architecture;

// The time-zone baselines and client date allow-lists as captured when the guard rails landed (16 Nov). Lanes only
// remove from them: ViewerTimeZoneTests fails when a lane file lists a (path, token) that is not here, a count above
// the captured one, or more lines than its lane's ceiling, and when an allow-list names a file that is not here.
// Never add to these lists; a new violation is fixed, not listed. TODO: remove with the baseline mechanism.
internal static class TimeZoneBaselineCapture
{
    // Lines each lane file held at capture; a lane may hold fewer, never more.
    public static readonly IReadOnlyDictionary<string, int> LaneLineCeilings = new Dictionary<string, int>
    {
        ["demo"] = 2,
        ["work-items"] = 13,
        ["inbox-asks"] = 9,
        ["agents-overview"] = 9,
        ["retros-timeline"] = 3,
        ["boards-admin"] = 16,
        ["closeout"] = 0,
    };

    // Every baseline line at capture: (lane, path, token, count).
    public static readonly IReadOnlyList<(string Lane, string Path, string Token, int Count)> Entries =
    [
        ("demo", "src/ShipLean.Application/Features/Demos/Reads/SprintDemoLabels.cs", "DisplayDate.Clock(", 1),
        ("demo", "src/ShipLean.Application/Features/Demos/Reads/SprintDemoLabels.cs", "LocalTimeZone", 1),
        ("work-items", "src/ShipLean.Application/Fakes/Attachments/InMemoryAttachmentStore.cs", "DisplayDate.Relative(value, clock)", 1),
        ("work-items", "src/ShipLean.Application/Fakes/Comments/InMemoryCommentStore.cs", "DisplayDate.Relative(value, clock)", 3),
        ("work-items", "src/ShipLean.Application/Fakes/WorkItems/InMemoryDtoBuilder.cs", "DisplayDate.Relative(value, clock)", 5),
        ("work-items", "src/ShipLean.Application/Fakes/WorkItems/InMemoryDtoBuilder.cs", "LocalTimeZone", 2),
        ("work-items", "src/ShipLean.Application/Features/Links/Rows/LinkCardRow.cs", "DisplayDate.Relative(value, clock)", 1),
        ("work-items", "src/ShipLean.Application/Features/WorkItemDetails/Attachments/AttachmentRow.cs", "DisplayDate.Relative(value, clock)", 1),
        ("work-items", "src/ShipLean.Application/Features/WorkItemDetails/Comments/CommentRow.cs", "DisplayDate.Relative(value, clock)", 3),
        ("work-items", "src/ShipLean.Application/Features/WorkItemDetails/History/HistoryEndpoints.cs", "DisplayDate.Relative(value, clock)", 1),
        ("work-items", "src/ShipLean.Application/Features/WorkItems/WorkItemLinkRow.cs", "DisplayDate.Relative(value, clock)", 1),
        ("work-items", "src/ShipLean.Application/Features/WorkItems/WorkItemReads.cs", "DisplayDate.Relative(value, clock)", 6),
        ("work-items", "src/ShipLean.Application/Features/WorkItems/WorkItemReads.cs", "LocalTimeZone", 2),
        ("work-items", "src/ShipLean.WebApi/Features/Mcp/Tools/Feature/VerifiedLine.cs", "DisplayDate.Relative(value, clock)", 1),
        ("work-items", "src/ShipLean.WebApi/Features/Mcp/Tools/Orient/GetWorkItemTool.cs", "LocalTimeZone", 2),
        ("inbox-asks", "src/ShipLean.Application/Features/Asks/History/AskHistory.cs", "DisplayDate.Relative(value, clock)", 1),
        ("inbox-asks", "src/ShipLean.Application/Features/Asks/Reads/ItemAsks.cs", "DisplayDate.Relative(value, clock)", 6),
        ("inbox-asks", "src/ShipLean.Application/Features/Asks/Waiting/WaitingOnMe.cs", "DisplayDate.Relative(value, clock)", 1),
        ("inbox-asks", "src/ShipLean.Application/Features/Inbox/AnsweredReader.cs", "DisplayDate.Relative(value, clock)", 2),
        ("inbox-asks", "src/ShipLean.Application/Features/Inbox/AnsweredReader.cs", "LocalTimeZone", 2),
        ("inbox-asks", "src/ShipLean.Application/Features/Inbox/AskInboxSource.cs", "DisplayDate.Relative(value, clock)", 2),
        ("inbox-asks", "src/ShipLean.Application/Features/Inbox/InboxReader.cs", "DisplayDate.Clock(", 1),
        ("inbox-asks", "src/ShipLean.Application/Features/Inbox/MentionInboxSource.cs", "DisplayDate.Relative(value, clock)", 1),
        ("inbox-asks", "src/ShipLean.Application/Features/Inbox/StatusLineInboxSource.cs", "DisplayDate.Relative(value, clock)", 1),
        ("agents-overview", "src/ShipLean.Application/Features/AgentDispatch/AgentPanelReader.cs", "DisplayDate.Relative(value, clock)", 4),
        ("agents-overview", "src/ShipLean.Application/Features/AgentDispatch/Rendering/AgentPromptContext.cs", "format \"yyyy-MM-dd HH:mm:ss\"", 1),
        ("agents-overview", "src/ShipLean.Application/Features/AgentDispatch/Rendering/AgentProposePromptContext.cs", "format \"yyyy-MM-dd HH:mm:ss\"", 1),
        ("agents-overview", "src/ShipLean.Application/Features/FeaturesOverview/DigestLines.cs", "DisplayDate.Relative(value, clock)", 1),
        ("agents-overview", "src/ShipLean.Application/Features/FeaturesOverview/FeatureDigest.cs", "DisplayDate.Relative(value, clock)", 1),
        ("agents-overview", "src/ShipLean.Application/Features/FeaturesOverview/FeaturesOverview.cs", "DisplayDate.Relative(value, clock)", 1),
        ("agents-overview", "src/ShipLean.Application/Features/FeaturesOverview/FeaturesSince.cs", "DisplayDate.Clock(", 2),
        ("agents-overview", "src/ShipLean.Application/Features/FeaturesOverview/FeaturesSince.cs", "DisplayDate.Weekday(", 1),
        ("agents-overview", "src/ShipLean.Application/Features/FeaturesOverview/FeaturesSince.cs", "LocalTimeZone", 1),
        ("retros-timeline", "src/ShipLean.Application/Features/Retros/Features/RetroFeaturesReader.cs", "DisplayDate.Relative(value, clock)", 1),
        ("retros-timeline", "src/ShipLean.Application/Features/Retros/Reads/RetroCards.cs", "DisplayDate.Relative(value, clock)", 2),
        ("retros-timeline", "src/ShipLean.Application/Features/Timeline/FeatureTimeline.cs", "LocalTimeZone", 1),
        ("boards-admin", "src/ShipLean.Application/Fakes/Agents/InMemoryAgentAuthorizations.cs", "DisplayDate.Relative(value, clock)", 2),
        ("boards-admin", "src/ShipLean.Application/Fakes/Import/InMemoryImportJobs.cs", "DisplayDate.Relative(value, clock)", 2),
        ("boards-admin", "src/ShipLean.Application/Fakes/Import/InMemoryImportJobs.cs", "format \"HH:mm:ss\"", 1),
        ("boards-admin", "src/ShipLean.Application/Features/Agents/Stores/AgentAuthorizations.cs", "DisplayDate.Relative(value, clock)", 2),
        ("boards-admin", "src/ShipLean.Application/Features/Backups/Admin/BackupAdmin.cs", "DisplayDate.Relative(value, clock)", 1),
        ("boards-admin", "src/ShipLean.Application/Features/Backups/Admin/BackupRunLabels.cs", "DisplayDate.Relative(value, clock)", 1),
        ("boards-admin", "src/ShipLean.Application/Features/Boards/View/CardExtraFields.cs", "DisplayDate.Relative(value, clock)", 2),
        ("boards-admin", "src/ShipLean.Application/Features/Import/Jira/JiraConnectionService.cs", "DisplayDate.Relative(value, clock)", 2),
        ("boards-admin", "src/ShipLean.Application/Features/Import/Jobs/ImportJobRows.cs", "DisplayDate.Relative(value, clock)", 2),
        ("boards-admin", "src/ShipLean.Application/Features/Import/Jobs/ImportJobRows.cs", "LocalTimeZone", 1),
        ("boards-admin", "src/ShipLean.Application/Features/Import/Jobs/ImportJobRows.cs", "TimeZoneInfo.ConvertTime", 1),
        ("boards-admin", "src/ShipLean.Application/Features/Import/Jobs/ImportJobRows.cs", "format \"HH:mm:ss\"", 1),
        ("boards-admin", "src/ShipLean.Application/Features/Taskboard/TaskboardCards.cs", "DisplayDate.Relative(value, clock)", 2),
        ("boards-admin", "src/ShipLean.Application/Features/Users/Admin/UserAdmin.cs", "DisplayDate.Relative(value, clock)", 1),
        ("boards-admin", "src/ShipLean.WebApi/Features/DeviceSessions/DeviceSessionDtos.cs", "DisplayDate.Relative(value, clock)", 1),
        ("boards-admin", "src/ShipLean.WebApi/Features/DeviceSessions/DeviceSessionDtos.cs", "LocalTimeZone", 1),
    ];

    // Files each app's eslint/dates-allowlist.json listed at capture (paths from the package root).
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ClientAllowLists = new Dictionary<string, IReadOnlyList<string>>
    {
        ["ShipLean.Client"] = ["src/features/sprints/backlog/sprintWorkingDays.ts"],
        ["ShipLean.Mobile"] = ["src/actions/InMemoryActionRunner.ts", "src/actions/outbox.ts", "src/voice/after/voiceChanges.ts"],
    };
}
