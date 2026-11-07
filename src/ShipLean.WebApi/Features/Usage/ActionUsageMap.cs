using ShipLean.Contracts.Usage;

namespace ShipLean.WebApi.Features.Usage;

// Every controller action, keyed "Controller.Action" (strings, so Usage does not reference other features), mapped to the event
// the filter emits, to SelfEmitted, or to Untracked with a reason. ActionUsageMapTests fails the build when an action is missing.
public static class ActionUsageMap
{
    private const string ScreenCounts = "A read: the screen view already counts it.";
    private const string Helper = "A helper call made while editing; it changes and opens nothing.";
    private const string Noise = "Noise: protocol or bulk traffic that other events already count.";

    public static readonly IReadOnlyDictionary<string, ActionUsage> All = Build();

    private static Dictionary<string, ActionUsage> Build()
    {
        Dictionary<string, ActionUsage> map = new(StringComparer.Ordinal);

        void Add(ActionUsage usage, params string[] actions)
        {
            foreach (var action in actions)
            {
                map.Add(action, usage);
            }
        }

        Add(ActionUsage.Track(UsageEventNames.AuthSignedOut), "Auth.Logout");
        Add(ActionUsage.Track(UsageEventNames.AuthPasswordChanged), "Auth.ChangePassword");
        Add(ActionUsage.Track(UsageEventNames.AuthOtherSessionsSignedOut), "Auth.SignOutOthers");
        Add(ActionUsage.Track(UsageEventNames.AgentRevoked), "ConnectedAgents.Revoke");
        Add(ActionUsage.Track(UsageEventNames.AgentSessionStarted), "AgentSession.Start");
        Add(ActionUsage.Track(UsageEventNames.AgentSessionStopped), "AgentSession.Stop");
        Add(ActionUsage.Track(UsageEventNames.ProjectLeft), "ProjectSettings.Leave");
        Add(ActionUsage.Track(UsageEventNames.ProjectSettingsChanged), "ProjectSettings.Put");
        Add(ActionUsage.Track(UsageEventNames.ProjectForecastChanged), "Forecast.PutOverride");
        Add(ActionUsage.Track(UsageEventNames.WorkItemCreated), "WorkItems.Create");
        Add(ActionUsage.Track(UsageEventNames.WorkItemViewed), "ProjectWorkItems.ByKey");
        Add(ActionUsage.Track(UsageEventNames.WorkItemUpdated), "WorkItems.Patch");
        Add(ActionUsage.Track(UsageEventNames.WorkItemStatusConfirmed), "WorkItemStatus.Confirm");
        Add(ActionUsage.Track(UsageEventNames.WorkItemMoved, board: "board"), "BoardView.Move");
        Add(ActionUsage.Track(UsageEventNames.WorkItemMoved, board: "feature_board"), "FeatureBoard.Move");
        Add(ActionUsage.Track(UsageEventNames.WorkItemMoved, board: "taskboard"), "Taskboard.Move");
        Add(ActionUsage.Track(UsageEventNames.WorkItemReordered), "WorkItems.Reorder");
        Add(ActionUsage.Track(UsageEventNames.WorkItemRemoved), "WorkItems.Remove");
        Add(ActionUsage.Track(UsageEventNames.BacklogBulkWritten), "BacklogWrites.Bulk");
        Add(ActionUsage.Track(UsageEventNames.BacklogItemsAdded), "BacklogWrites.Add");
        Add(ActionUsage.Track(UsageEventNames.SearchPerformed), "Search.Get");
        Add(ActionUsage.Track(UsageEventNames.CommentAdded), "WorkItemComments.Add");
        Add(ActionUsage.Track(UsageEventNames.CommentEdited), "Comments.Edit");
        Add(ActionUsage.Track(UsageEventNames.CommentSuperseded), "Comments.Supersede");
        Add(ActionUsage.Track(UsageEventNames.CommentArchived), "Comments.Archive");
        Add(ActionUsage.Track(UsageEventNames.AttachmentAdded), "WorkItemAttachments.Upload");
        Add(ActionUsage.Track(UsageEventNames.AttachmentViewed), "Attachments.Download", "AttachmentMarkdown.View");
        Add(ActionUsage.Track(UsageEventNames.AttachmentArchived), "Attachments.Archive");
        Add(ActionUsage.Track(UsageEventNames.LinkAdded), "WorkItemLinks.Add");
        Add(ActionUsage.Track(UsageEventNames.LinkCardAdded), "LinkCards.Add");
        Add(ActionUsage.Track(UsageEventNames.LinkCardEdited), "LinkCards.Edit");
        Add(ActionUsage.Track(UsageEventNames.LinkCardArchived), "LinkCards.Archive");
        Add(ActionUsage.Track(UsageEventNames.LinkCardRefreshed), "LinkCardsRefresh.Refresh");
        Add(ActionUsage.Track(UsageEventNames.SprintCreated), "ProjectSprints.Create");
        Add(ActionUsage.Track(UsageEventNames.SprintUpdated), "Sprints.Update");
        Add(ActionUsage.Track(UsageEventNames.SprintCapacityChanged), "SprintCapacity.Put");
        Add(ActionUsage.Track(UsageEventNames.SprintRolledOver), "SprintRollover.Apply");
        Add(ActionUsage.Track(UsageEventNames.BoardSettingsChanged), "BoardSettings.Put");
        Add(ActionUsage.Track(UsageEventNames.RetroPointAdded), "RetroPoints.Add");
        Add(ActionUsage.Track(UsageEventNames.RetroSearched), "RetroSearch.Search");
        Add(ActionUsage.Track(UsageEventNames.RetroPointArchived), "RetroPoints.Archive");
        Add(ActionUsage.Track(UsageEventNames.RetroPointMoved), "RetroPoints.MoveTheme");
        Add(ActionUsage.Track(UsageEventNames.RetroActionCreated), "RetroActions.Create");
        Add(ActionUsage.Track(UsageEventNames.RetroActionPointsAdded), "RetroActions.AddPoints");
        Add(ActionUsage.Track(UsageEventNames.RetroActionUpdated), "RetroActions.Update");
        Add(ActionUsage.Track(UsageEventNames.RetroThemesMerged), "RetroThemeWrites.Merge", "RetroMergeProposals.Merge");
        Add(ActionUsage.Track(UsageEventNames.RetroMergeKeptApart), "RetroMergeProposals.KeepApart");
        Add(ActionUsage.Track(UsageEventNames.RetroThemeRenamed), "RetroThemeWrites.Rename");
        Add(ActionUsage.Track(UsageEventNames.RetroPointNotNeeded), "RetroDecisions.NotNeeded");
        Add(ActionUsage.Track(UsageEventNames.RetroNotNeededUndone), "RetroDecisions.UndoNotNeeded");
        Add(ActionUsage.Track(UsageEventNames.ImportStarted), "ImportJobs.Start");
        Add(ActionUsage.Track(UsageEventNames.ImportJobChanged), "ImportJobs.Rerun", "ImportJobs.Retry", "ImportJobs.Skip", "ImportJobs.Stop");
        Add(ActionUsage.Track(UsageEventNames.ImportJiraConnected), "Jira.Put");
        Add(ActionUsage.Track(UsageEventNames.ProfileUpdated), "Profile.Update");
        Add(ActionUsage.Track(UsageEventNames.ProfilePhotoChanged), "Profile.ReplacePhoto", "Profile.RemovePhoto");
        Add(ActionUsage.Track(UsageEventNames.SprintDemoCreated), "SprintDemo.Create");
        Add(ActionUsage.Track(UsageEventNames.SprintDemoYouTubeRetried), "SprintDemo.RetryYouTube");
        Add(ActionUsage.Track(UsageEventNames.ProjectDemoSettingsChanged), "ProjectDemoSettings.Put");
        Add(ActionUsage.Track(UsageEventNames.ProjectDemoBrandChanged), "DemoBrand.ReplaceLogo", "DemoBrand.RemoveLogo", "DemoBrand.PutTokens");
        Add(ActionUsage.Track(UsageEventNames.ProjectYouTubeChanged), "YouTube.Check", "YouTube.Disconnect");
        Add(ActionUsage.Track(UsageEventNames.InstanceSettingsChanged), "Instance.PutSettings");
        Add(ActionUsage.Track(UsageEventNames.InstanceIntegrationsChanged), "Integrations.Put");
        Add(ActionUsage.Track(UsageEventNames.InstanceAnalyticsChanged), "Analytics.Put");
        Add(ActionUsage.Track(UsageEventNames.InstanceAgentConnectionCreated), "AgentConnections.Create");
        Add(
            ActionUsage.Track(UsageEventNames.InstanceAgentConnectionChanged),
            "AgentConnections.Update",
            "AgentConnections.Archive",
            "AgentConnections.RegenerateSecret",
            "AgentConnections.RegenerateToken");
        Add(ActionUsage.Track(UsageEventNames.InstanceUserAdded), "UsersAdmin.Add");
        Add(
            ActionUsage.Track(UsageEventNames.InstanceUserChanged),
            "UsersAdmin.ChangeEmail",
            "UsersAdmin.IssueOneTimePassword",
            "UsersAdmin.SetDisabled",
            "UsersAdmin.SetInstanceAdmin",
            "UsersAdmin.Unlock");
        Add(ActionUsage.Track(UsageEventNames.InstanceSearchReindexed), "SearchAdmin.Reindex");

        // Self-emitted: the action knows what the filter cannot (which sign-in, which decision, the new project, the screen, the sort).
        Add(ActionUsage.SelfEmitted, "Auth.Login", "Consent.Decide", "ProjectCreation.Create", "Instance.Join", "Usage.Screen",
            "ProjectUsage.BacklogSort");

        Add(
            ActionUsage.Untracked(Helper),
            "Markdown.Render",
            "AttachmentCheck.Check",
            "ImportMapping.Suggest",
            "LinkCards.Preview",
            "Jira.Test",
            "ProjectCreation.KeyCheck",
            "FeatureSlug.Check",
            "BacklogWrites.Duplicates",
            "Analytics.Test");
        Add(
            ActionUsage.Untracked(Noise),
            "Preferences.Get",
            "Preferences.Put",
            "Preferences.ResetViews",
            "OAuthToken.Token",
            "OAuthRevoke.Revoke",
            "OAuthAuthorize.Authorize",
            "ClientRegistration.Register",
            "AuthorizationServerMetadata.Get",
            "Uploads.Upload",
            "Downloads.Download",
            "LinkCardImages.Favicon",
            "LinkCardImages.Image");
        Add(ActionUsage.Untracked("Anonymous, one-time upload token: the delivery that follows publishes SprintDemoChanged, and the demo's creation is counted by SprintDemo.Create."), "DemoUpload.Upload");
        Add(ActionUsage.Untracked("Only builds Google's consent address; nothing changes until the callback and the check, and the check is counted by YouTube.Check."), "YouTube.Start");
        Add(ActionUsage.Untracked("Anonymous browser redirect from Google with no session of its own; the connection it stores is counted by YouTube.Check when the person confirms it."), "YouTube.Callback");
        Add(ActionUsage.Untracked("Anonymous, token-authenticated download of the project's logo for the agent making a demo; the demo's creation is counted by SprintDemo.Create."), "DemoLogoDownload.Download");
        Add(ActionUsage.Untracked("Anonymous, signature-authenticated webhook: counted as a system event where it is handled."), "Webhooks.GitHub", "Webhooks.GitLab");
        Add(ActionUsage.Untracked("The item detail view loads the item by key (ProjectWorkItems.ByKey), which is the one that counts; tracking both would count a view twice."), "WorkItems.Get");
        Add(
            ActionUsage.Untracked(ScreenCounts),
            "AgentConnections.List",
            "AgentSession.Availability",
            "AgentSession.Sessions",
            "Analytics.Get",
            "Auth.Instance",
            "GoogleSignIn.Start",
            "BoardSettings.Get",
            "BoardView.Get",
            "DemoBrand.Logo",
            "ConnectedAgents.List",
            "Consent.Get",
            "FeatureBoardRead.Get",
            "Holidays.Countries",
            "ImportJobs.Errors",
            "ImportJobs.Get",
            "ImportJobs.List",
            "ImportJobs.ListForProject",
            "ImportJobs.Log",
            "Instance.GetSettings",
            "Instance.ListProjects",
            "Integrations.Get",
            "Jira.Get",
            "Jira.Projects",
            "LevelBacklog.Get",
            "LinkCards.List",
            "McpUrl.Get",
            "Me.Get",
            "ProjectBoards.List",
            "ProjectCreation.FirstSprint",
            "ProjectForecast.Get",
            "ProjectDemoSettings.Get",
            "Projects.ByKey",
            "Projects.Get",
            "Projects.List",
            "Projects.Members",
            "Projects.Sprints",
            "Projects.States",
            "ProjectSettings.Get",
            "ProjectSprints.Next",
            "ProjectVelocity.Get",
            "ProjectWorkItems.List",
            "ProjectWorkItems.Tags",
            "RetroActionDialog.Get",
            "RetroActions.Get",
            "RetroFeatures.List",
            "RetroInsights.Get",
            "RetroItems.Get",
            "RetroThemes.List",
            "SprintCapacity.Get",
            "SprintDemo.Get",
            "SprintMetrics.Get",
            "SprintRetro.Get",
            "SprintRollover.Get",
            "StoriesBacklog.Get",
            "Taskboard.Get",
            "UserPhoto.Get",
            "Users.ByEmail",
            "UsersAdmin.List",
            "WorkItemAttachments.List",
            "WorkItemChildren.List",
            "WorkItemComments.List",
            "WorkItemHistory.Get",
            "WorkItemRetros.Get");

        return map;
    }
}
