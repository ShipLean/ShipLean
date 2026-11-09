using System.Reflection;

namespace ShipLean.Contracts.Usage;

// The event catalogue. Names match ^[a-z]+(_[a-z]+)*\.[a-z]+(_[a-z]+)*$ and are at most 50 characters (Umami's limit).
// analytics.test is not here: it never goes through Track.
public static class UsageEventNames
{
    public const string AuthSignedIn = "auth.signed_in";
    public const string AuthSignedOut = "auth.signed_out";
    public const string AuthPasswordChanged = "auth.password_changed";
    public const string AuthOtherSessionsSignedOut = "auth.other_sessions_signed_out";
    public const string AgentConnected = "agent.connected";
    public const string AgentConsentDenied = "agent.consent_denied";
    public const string AgentRevoked = "agent.revoked";
    public const string AgentSessionStarted = "agent_session.started";
    public const string AgentSessionStopped = "agent_session.stopped";
    public const string AgentSessionOpened = "agent_session.opened";
    public const string AgentSessionFinished = "agent_session.finished";
    public const string ProjectCreated = "project.created";
    public const string ProjectJoined = "project.joined";
    public const string ProjectLeft = "project.left";
    public const string ProjectSettingsChanged = "project.settings_changed";
    public const string ProjectForecastChanged = "project.forecast_changed";
    public const string ProjectsListed = "projects.listed";
    public const string WorkItemCreated = "work_item.created";
    public const string WorkCaptured = "work.captured";
    public const string WorkItemBrokenDown = "work_item.broken_down";
    public const string WorkItemViewed = "work_item.viewed";
    public const string WorkItemUpdated = "work_item.updated";
    public const string WorkItemStatusSet = "work_item.status_set";
    public const string WorkItemStatusConfirmed = "work_item.status_confirmed";
    public const string WorkItemProgressUpdated = "work_item.progress_updated";
    public const string WorkItemMoved = "work_item.moved";
    public const string WorkItemMovedToSprint = "work_item.moved_to_sprint";
    public const string WorkItemReordered = "work_item.reordered";
    public const string WorkItemRemoved = "work_item.removed";
    public const string TaskPickedUp = "task.picked_up";
    public const string TaskHandedBack = "task.handed_back";
    public const string TaskReported = "task.reported";
    public const string PickupListed = "pickup.listed";
    public const string BriefViewed = "brief.viewed";
    public const string PlanCaptured = "plan.captured";
    public const string PlanStatusViewed = "plan.status_viewed";
    public const string PlanningGuideViewed = "planning_guide.viewed";
    public const string FeatureProgressViewed = "feature.progress_viewed";
    public const string BacklogReviewed = "backlog.reviewed";
    public const string BacklogBulkWritten = "backlog.bulk_written";
    public const string BacklogItemsAdded = "backlog.items_added";
    public const string BacklogSorted = "backlog.sorted";
    public const string ChangesViewed = "changes.viewed";
    public const string SearchPerformed = "search.performed";
    public const string CommentAdded = "comment.added";
    public const string CommentEdited = "comment.edited";
    public const string CommentSuperseded = "comment.superseded";
    public const string CommentArchived = "comment.archived";
    public const string AttachmentAdded = "attachment.added";
    public const string AttachmentViewed = "attachment.viewed";
    public const string AttachmentArchived = "attachment.archived";
    public const string PayloadUploadIssued = "payload_upload.issued";
    public const string LinkAdded = "link.added";
    public const string LinkStateReported = "link.state_reported";
    public const string LinkCardAdded = "link_card.added";
    public const string LinkCardEdited = "link_card.edited";
    public const string LinkCardArchived = "link_card.archived";
    public const string LinkCardRefreshed = "link_card.refreshed";
    public const string SprintCreated = "sprint.created";
    public const string SprintPlanned = "sprint.planned";
    public const string SprintUpdated = "sprint.updated";
    public const string SprintDeleted = "sprint.deleted";
    public const string SprintCapacityChanged = "sprint.capacity_changed";
    public const string SprintEnded = "sprint.ended";
    public const string SprintRolledOver = "sprint.rolled_over";
    public const string SprintStatusViewed = "sprint.status_viewed";
    public const string SprintReviewed = "sprint.reviewed";
    public const string BoardSettingsChanged = "board.settings_changed";
    public const string RetroPointAdded = "retro.point_added";
    public const string RetroListed = "retro.listed";
    public const string RetroSearched = "retro.searched";
    public const string RetroPointArchived = "retro.point_archived";
    public const string RetroPointMoved = "retro.point_moved";
    public const string RetroActionCreated = "retro.action_created";
    public const string RetroActionPointsAdded = "retro.action_points_added";
    public const string RetroActionUpdated = "retro.action_updated";
    public const string RetroThemesMerged = "retro.themes_merged";
    public const string RetroThemeRenamed = "retro.theme_renamed";
    public const string RetroPointNotNeeded = "retro.point_not_needed";
    public const string RetroNotNeededUndone = "retro.not_needed_undone";
    public const string RetroMergeKeptApart = "retro.merge_kept_apart";
    public const string RetroActionsDecided = "retro.actions_decided";
    public const string ImportStarted = "import.started";
    public const string ImportJobChanged = "import.job_changed";
    public const string ImportJiraConnected = "import.jira_connected";
    public const string ImportCompleted = "import.completed";
    public const string ImportFailed = "import.failed";
    public const string WebhookReceived = "webhook.received";
    public const string ProfileUpdated = "profile.updated";
    public const string ProfilePhotoChanged = "profile.photo_changed";
    public const string InstanceSettingsChanged = "instance.settings_changed";
    public const string InstanceIntegrationsChanged = "instance.integrations_changed";
    public const string InstanceAnalyticsChanged = "instance.analytics_changed";
    public const string InstanceAgentConnectionCreated = "instance.agent_connection_created";
    public const string InstanceAgentConnectionChanged = "instance.agent_connection_changed";
    public const string InstanceUserAdded = "instance.user_added";
    public const string InstanceUserChanged = "instance.user_changed";
    public const string InstanceSearchReindexed = "instance.search_reindexed";
    public const string ScreenViewed = "screen.viewed";
    public const string SprintDemoCreated = "sprint_demo.created";
    public const string SprintDemoYouTubeRetried = "sprint_demo.youtube_retried";
    public const string ProjectDemoSettingsChanged = "project_demo.settings_changed";
    public const string IssueIntakeSettingsChanged = "issue_intake.settings_changed";
    public const string ProjectDemoBrandChanged = "project_demo.brand_changed";
    public const string ProjectYouTubeChanged = "project_youtube.changed";
    public const string BackupSettingsSaved = "backup.settings_saved";
    public const string BackupConnectionTested = "backup.connection_tested";
    public const string BackupRunStarted = "backup.run_started";

    // System: the backup job emits it when a run ends, whatever started it.
    public const string BackupRunFinished = "backup.run_finished";

    public static readonly IReadOnlySet<string> All = typeof(UsageEventNames)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);
}
