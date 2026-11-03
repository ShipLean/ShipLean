# Usage analytics

ShipLean can send usage events to Umami or PostHog so an operator can see what people and agents do. It is off by default and chosen per instance in Instance settings → Analytics. Delivery is server-side only: the browser loads no analytics script, so there is no cookie banner, no ad-blocker loss and no CSP change.

The code is the source of truth. `UsageEventNames`, `ActionUsageMap` and `ToolUsageMap` define what is tracked; the table below is a copy of their current state and can go stale.

## What is tracked

- **Events count actions, not objects.** `work.captured` is one event even if it made five items. Object counts live in the database.
- **People and agents are tracked side by side.** The same action has the same event name whether a person did it in the web app or an agent did it over MCP. Every event says which kind of actor did it (`actor_kind`: `user`, `agent` or `system`) and on which surface (`surface`: `web`, `mcp` or `system`).
- **Every event carries its project** (`project_id`, `project_key`) when the request touched exactly one project, and `project_count` says how many it touched.
- **What is not tracked:** reads that a screen view already counts, helper calls, protocol noise and webhooks (counted as system events where handled). Each is listed in `ActionUsageMap` with a reason. Requests refused before the action runs (a 403 from the MCP gate, a web authorization failure, a model-binding failure) never reach a capture filter and are not tracked.

## How capture works

Three seams, so a feature never has to remember to track itself:

1. **MCP tools.** `ToolUsageFilter` (`src/ShipLean.WebApi/Features/Mcp/Usage/ToolUsageFilter.cs`) wraps every tool call. It looks the tool up in `ToolUsageMap` and emits one event with the agent as actor, the outcome, the error code and the duration. A tool with no entry emits nothing. A canceled call has outcome `cancelled`.
2. **Web actions.** `UsageActionFilter` (`src/ShipLean.WebApi/Features/Usage/UsageActionFilter.cs`) is a global MVC action filter. It looks `Controller.Action` up in `ActionUsageMap`, whose entries are `Track` (the filter emits the event), `SelfEmitted` (the action emits its own) or `Untracked` (with a reason). Anonymous requests emit nothing.
3. **Direct `Track` calls**, only where neither filter can see the fact: system events (`agent_session.*`, `import.*`, `webhook.received`), `screen.viewed` (`ScreenViews`), and the self-emitted web actions, which use `SelfEmittedUsage.TrackOk` (`src/ShipLean.WebApi/Infrastructure/SelfEmittedUsage.cs`).

**The project stamp.** `UsageStampingProjectAccess` (`src/ShipLean.Application/Identity/UsageStampingProjectAccess.cs`) decorates `IProjectAccess`. Every project-scoped command already calls `RequireAsync`, so after a successful check it tells `IUsageScope` which project the request touched. The filters read `IUsageScope.ProjectId` (set only when exactly one distinct project was touched) and `ProjectCount`. Neither filter parses routes or arguments. A refused check throws before the stamp, so a project the caller cannot see is never stamped. `IUsageScope` does nothing outside a plain HTTP request.

**Events that carry no project:**

- `search.performed`, `projects.listed`, `auth.*`, `instance.*`, `profile.*`, `agent.connected`, `agent.consent_denied` and `agent.revoked` touch no single project. (`project.created` and `project.joined` run no access check, so they are self-emitted with the project's id and do carry it.)
- Any event whose request touched several projects (the id is left out, `project_count` is above 1) or none.

## Delivery

`IUsageEvents.Track` never blocks and never throws. `UsageQueue` (`src/ShipLean.Application/Features/Usage/UsageQueue.cs`) is a bounded in-memory queue; `UsageDispatcher` (`src/ShipLean.Application/Features/Usage/UsageDispatcher.cs`) is the only reader.

- The queue holds 10,000 events. When it is full, events are dropped and counted on `shiplean.usage.dropped`.
- A batch opens when an event arrives and is flushed after 2 s, or at 100 events. The dispatcher does not run a timer while the queue is empty.
- Settings are read when events arrive and cached for 30 s, so a change in the Analytics tab applies within about 30 s.
- A failed send is retried once after 5 s, then the batch is dropped. `shiplean.analytics.sent` and `shiplean.analytics.failed` count events, tagged by `provider`. A warning is logged at most once a minute per provider; it never contains the site key.
- On shutdown the dispatcher tries to send what is queued for at most 5 seconds; anything not sent by then is lost.
- Nothing is persisted. A restart loses the queue.

The dispatcher maps each `UsageEvent` to an `AnalyticsEvent` (`UsageEventMapper`) and hands the batch to the sink of the configured provider (`IAnalyticsSink`: `UmamiSink`, `PostHogSink`). A sink throws `AnalyticsSendException` on any failure.

### Standard properties

Every event carries these (a key is left out when its value is null). The constants are in `AnalyticsProps` (`src/ShipLean.Contracts/Analytics/AnalyticsProps.cs`).

| Property | Meaning |
|---|---|
| `surface`, `actor_kind` | `web` / `mcp` / `system`; `user` / `agent` / `system` |
| `user_id` | the user (for an agent, the user it acts for) |
| `agent_id`, `agent_name`, `agent_host`, `agent_verified` | agent actors only; name and host are client-supplied and cut to 64 characters |
| `project_id`, `project_key`, `project_count` | see above |
| `outcome`, `error_code` | `ok` / `error` / `cancelled`; the `ApiException` code on error |
| `duration_ms`, `source` | `source` is `tool:<name>`, `api:<Controller>.<Action>`, `screen` or `system:<Class>` |
| `shiplean_version`, `instance_host` | this build and this instance |

Extra properties are the keys in `UsageProps` (`screen`, `board`, `provider`, `count`, `agent_name` on `agent.connected` and `agent.consent_denied`). A standard property wins over an extra one with the same key.

The distinct id of an actor is `user:<guid>`, `agent:<authorizationId>` (`agent:gw-<userId>` for the gateway) or `system`. Person properties (`kind`, `user_id`, `agent_*`) go to the provider as an identify (Umami) or `$set` (PostHog). `name` and `email` are added for users only when the instance's **Send people's names** toggle is on, which is off by default.

### The rules for properties

- **No free text, ever.** Never put a title, description, comment, search query, attachment name, project name, state name or user-typed URL into an event. Values are ids, catalogue names, enum-like strings, numbers and booleans. The only places a name can leave the server are the people's names above and `agent_name` / `agent_host`, which identify a client, not content.
- Property keys come from `UsageProps` and `AnalyticsProps`, never literals. Values are `string` (200 characters at most), `long`, `double` or `bool`.
- Never let analytics slow or fail a request: no `await` on delivery in a request path, no exception escapes `Track`.
- Never log a site key in full (the last 4 characters at most).

## Providers

| | Umami | PostHog |
|---|---|---|
| Endpoint | `POST {host}/api/batch` | `POST {host}/batch/` |
| Site key | the website ID (a GUID) | the project API key (`phc_...`) |
| Actor | `id` on each item; `tag` is the actor kind | `distinct_id`; `$set` carries the person on an actor's first event and when it changes |
| Project | in the URL path `/p/{project_key or _}/{surface}/{name with . as /}`, and as a property | as a property, and `$groups.project` |
| Batches | at most 500 items per request; at most 50 properties per item, strings cut to 500 characters | one request per dispatcher batch |

Umami only filters on event properties on its Events page, which is why the project goes into the URL path and the actor kind into `tag`: both can be filtered in every Umami report. PostHog group analytics is a paid add-on, so the project also goes as a plain property.

**Umami must run with `DISABLE_BOT_CHECK=1`.** Every request comes from a server, and by default Umami excludes what it takes for bots. Umami also requires a `User-Agent` header on `/api/batch`; ShipLean sends `ShipLean/{version}`. Umami accepts a `payload.timestamp`, but ShipLean does not send one, so events are dated on arrival and a delayed or retried batch is dated when it lands.

Sources: [Umami: sending stats](https://docs.umami.is/docs/api/sending-stats) (`/api/batch` takes a JSON array, requires a `User-Agent`), [Umami environment variables](https://docs.umami.is/docs/environment-variables) (`DISABLE_BOT_CHECK`), [PostHog capture API](https://posthog.com/docs/api/capture) (`/batch/`, `api_key`, `batch`, `historical_migration`).

## Settings

Stored on `instance_settings` (`analytics_provider`, `analytics_host`, `analytics_site_key`, `analytics_send_names`), edited in Instance settings → Analytics by an instance admin through `GET` and `PUT api/instance/analytics`. The host must be an absolute `http` or `https` address with no query, fragment or credentials. An Umami site key is a GUID; a PostHog key starts with `phc_`. Provider `none` clears host and key.

**Send test event** calls `POST api/instance/analytics/test`. It sends one `analytics.test` event straight to the sink, with a 10 s timeout, and reports `ok` and a message. It does not go through the queue, and `analytics.test` is not in `UsageEventNames`.

## Add a tracked action

1. **Name the event.** If an existing name fits (the same action on the other surface), reuse it. Otherwise add a `public const string` to `UsageEventNames` (`src/ShipLean.Contracts/Usage/UsageEventNames.cs`): `area.verb_past`, lowercase with underscores, matching `^[a-z]+(_[a-z]+)*\.[a-z]+(_[a-z]+)*$`, at most 50 characters. `All` is built by reflection, so nothing else lists it.
2. **Map it.**
   - A new MCP tool: add `["tool_name"] = UsageEventNames.X` to `ToolUsageMap` (`src/ShipLean.WebApi/Features/Mcp/Usage/ToolUsageMap.cs`).
   - A new controller action: add it to `ActionUsageMap` (`src/ShipLean.WebApi/Features/Usage/ActionUsageMap.cs`), keyed `Controller.Action` without the `Controller` suffix. Use `ActionUsage.Track(name)` (add `board:` only for `work_item.moved`), `ActionUsage.SelfEmitted` if the action tracks itself, or `ActionUsage.Untracked("reason")`.
   - A self-emitted action: mark it `ActionUsage.SelfEmitted` and call `SelfEmittedUsage.TrackOk` (`src/ShipLean.WebApi/Infrastructure/SelfEmittedUsage.cs`) from the action.
   - A system event: call `IUsageEvents.Track` where the thing happens, with `UsageSurface.System` and `UsageActor.System`. `AgentTrigger` (`src/ShipLean.Application/Features/AgentDispatch/AgentTrigger.cs`) is the example.
3. **Keep the project stamp working.** A project-scoped command must call `IProjectAccess.RequireAsync` (it does already, by the access rule in `docs/auth.md`). Nothing else is needed for the project to appear.
4. **Add extra properties only from `UsageProps`.** A new key is a contract change: add the constant, and see the rules above. The `UsageEvent` constructor throws `ArgumentException` in the caller for an unknown event name, an extra key that is not in `UsageProps`, or a value of the wrong type (a string of at most 200 characters, `long`, `int`, `double` or `bool`; `int` is accepted).
5. **Test it** (below), and update the event catalogue table in this file in the same change.

**What the coverage tests enforce** (`src/ShipLean.UnitTest/Usage/`):

- `ActionUsageMapTests`: every public controller action is in the map; every `Track` name is in `UsageEventNames`; every `Untracked` entry has a reason; no key points at an action that no longer exists.
- `ToolUsageMapTests`: every `[McpServerTool]` is in the map; every mapped name is in the catalogue; no entry names a tool that no longer exists.
- `UsageEventNamesTests`: names match the pattern, fit 50 characters and are unique.
- `ScreenIdsTests`: `ScreenIds.All` matches the route ids in `src/ShipLean.Client/src/routes.ts`. A new client route needs a new entry in `ScreenIds` (`src/ShipLean.Contracts/Usage/ScreenIds.cs`).

A new tool or action with no mapping fails a unit test, not the build. A catalogue name that nothing emits does not: check it yourself.

## Test it

The test host replaces `IUsageEvents` with `RecordingUsageEvents` (`src/ShipLean.TestSupport/Usage/RecordingUsageEvents.cs`) for every fixture. Get it with `(RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>()`, so tests read what was tracked and never touch the queue or a provider.

- `Events` is a snapshot, `Clear()` empties it, and `WaitForAsync(predicate, timeout)` completes on a signal when a matching event arrives (or already has). `ApiFixture` shares one host across a fixture's tests, so call `Clear()` in a `[SetUp]`.
- Assert the fields that matter: `Name`, `Surface`, `Actor`, `ProjectId`, `ProjectCount`, `Outcome`, `ErrorCode`, `Source`, `Properties`. Make a negative test (an untracked action emits nothing) fail against the obvious wrong implementation.
- Examples: `src/ShipLean.WebApi.Tests/Usage/Web/UsageWebTests.cs` (web actions) and `src/ShipLean.WebApi.Tests/Usage/Mcp/ToolUsageTests.cs` (tools).

To test delivery, opt into the real pipeline: in `ConfigureTestServices` call `UsageServices.Register(services)`, register a `TimeProvider` and an `IAnalyticsSettingsReader` (use `FixedAnalyticsSettingsReader`, `src/ShipLean.TestSupport/Usage/FixedAnalyticsSettingsReader.cs`, and set its `Target`), and replace the sinks with `RecordingAnalyticsSink` (`FailWith`, `Calls`, `Events`). Sink tests use `AnalyticsSinkStubHandler` (`Respond`, `Requests`) as the `HttpMessageHandler`. See `src/ShipLean.WebApi.Tests/Usage/Delivery/UsageDispatcherTests.cs` and the sink tests under `src/ShipLean.WebApi.Tests/Analytics/`.

## Run against a local Umami

Umami ships a Docker Compose file with the app and a PostgreSQL database ([install guide](https://docs.umami.is/docs/install), [compose file](https://github.com/umami-software/umami/blob/master/docker-compose.yml)). Take it, add `DISABLE_BOT_CHECK: "1"` to the `umami` service's `environment`, set your own `APP_SECRET`, and start it:

```
docker compose up -d
```

1. Open <http://localhost:3000> and sign in (`admin` / `umami` on a fresh install; change it).
2. Settings → Websites → add a website, and copy its Website ID.
3. Start ShipLean (`dotnet run --project src/ShipLean.LocalHost -- --database shiplean_analytics --port 0`, then wait for `SHIPLEAN_LOCAL_URL=`), sign in as an instance admin, and open Instance settings → Analytics.
4. Pick Umami, host `http://localhost:3000`, site key the Website ID. Save, then **Send test event**. It should appear on the website's Events page within seconds.
5. Do something in the app. Events arrive about 2 s after a batch opens. Use the Events page, and filter by the URL path or by tag to see one project or one kind of actor.

If the ShipLean process runs in a container, `localhost` is the container: use the Umami container's name or `host.docker.internal`.

## Event catalogue

A copy of the current maps. `UsageEventNames` and the two maps are the source of truth.

| Event | MCP tools | Web actions (`Controller.Action`), self-emitted and system |
|---|---|---|
| `auth.signed_in` |  | self-emitted: Auth.Login, ProxySignInHandler |
| `auth.signed_out` |  | `Auth.Logout` |
| `auth.password_changed` |  | `Auth.ChangePassword` |
| `auth.other_sessions_signed_out` |  | `Auth.SignOutOthers` |
| `agent.connected` |  | self-emitted: Consent.Decide (allow) |
| `agent.consent_denied` |  | self-emitted: Consent.Decide (deny) |
| `agent.revoked` |  | `ConnectedAgents.Revoke` |
| `agent_session.started` |  | `AgentSession.Start` |
| `agent_session.stopped` |  | `AgentSession.Stop` |
| `agent_session.opened` |  | system: AgentTrigger |
| `agent_session.finished` |  | system: AgentSessionNotifier |
| `project.created` |  | self-emitted: ProjectCreation.Create |
| `project.joined` |  | self-emitted: Instance.Join |
| `project.left` |  | `ProjectSettings.Leave` |
| `project.settings_changed` |  | `ProjectSettings.Put` |
| `project.forecast_changed` |  | `Forecast.PutOverride` |
| `projects.listed` | `list_projects` |  |
| `work_item.created` | `create_feature` | `WorkItems.Create` |
| `work.captured` | `capture_work` |  |
| `work_item.broken_down` | `break_down` |  |
| `work_item.viewed` | `get_work_item` | `ProjectWorkItems.ByKey` |
| `work_item.updated` | `refine_work_item` | `WorkItems.Patch` |
| `work_item.status_set` | `set_status` |  |
| `work_item.status_confirmed` |  | `WorkItemStatus.Confirm` |
| `work_item.progress_updated` | `update_progress` |  |
| `work_item.moved` |  | `BoardView.Move`, `FeatureBoard.Move`, `Taskboard.Move` |
| `work_item.moved_to_sprint` | `move_to_sprint` |  |
| `work_item.reordered` | `reorder` | `WorkItems.Reorder` |
| `work_item.removed` |  | `WorkItems.Remove` |
| `task.picked_up` | `pick_up` |  |
| `task.handed_back` | `hand_back` |  |
| `task.reported` | `report_task` |  |
| `pickup.listed` | `find_work_to_pick_up` |  |
| `brief.viewed` | `get_brief` |  |
| `plan.captured` | `capture_plan` |  |
| `plan.status_viewed` | `plan_status` |  |
| `planning_guide.viewed` | `planning_guide` |  |
| `feature.progress_viewed` | `feature_progress` |  |
| `backlog.reviewed` | `review_backlog` |  |
| `changes.viewed` | `whats_changed_since` |  |
| `search.performed` | `search` | `Search.Get` |
| `comment.added` | `comment` | `WorkItemComments.Add` |
| `comment.edited` |  | `Comments.Edit` |
| `comment.superseded` |  | `Comments.Supersede` |
| `comment.archived` |  | `Comments.Archive` |
| `attachment.added` | `attach_markdown`, `attach_file` | `WorkItemAttachments.Upload` |
| `attachment.viewed` | `read_attachment` | `Attachments.Download`, `AttachmentMarkdown.View` |
| `attachment.archived` |  | `Attachments.Archive` |
| `link.added` | `link` | `WorkItemLinks.Add` |
| `link.state_reported` | `report_link_state` |  |
| `link_card.added` |  | `LinkCards.Add` |
| `link_card.edited` |  | `LinkCards.Edit` |
| `link_card.archived` |  | `LinkCards.Archive` |
| `link_card.refreshed` |  | `LinkCardsRefresh.Refresh` |
| `sprint.created` | `create_sprint` | `ProjectSprints.Create` |
| `sprint.planned` | `plan_sprint` |  |
| `sprint.updated` |  | `Sprints.Update` |
| `sprint.capacity_changed` |  | `SprintCapacity.Put` |
| `sprint.ended` | `end_sprint` |  |
| `sprint.rolled_over` |  | `SprintRollover.Apply` |
| `sprint.status_viewed` | `sprint_status` |  |
| `sprint.reviewed` | `sprint_review` |  |
| `board.settings_changed` |  | `BoardSettings.Put` |
| `retro.point_added` | `log_retrospective` | `RetroPoints.Add` |
| `retro.listed` | `list_retrospectives` |  |
| `retro.searched` |  | `RetroSearch.Search` |
| `retro.point_archived` |  | `RetroPoints.Archive` |
| `retro.point_moved` |  | `RetroPoints.MoveTheme` |
| `retro.action_created` |  | `RetroActions.Create` |
| `retro.action_points_added` |  | `RetroActions.AddPoints` |
| `retro.action_updated` |  | `RetroActions.Update` |
| `retro.themes_merged` |  | `RetroThemeWrites.Merge`, `RetroMergeProposals.Merge` |
| `retro.theme_renamed` |  | `RetroThemeWrites.Rename` |
| `retro.point_not_needed` |  | `RetroDecisions.NotNeeded` |
| `retro.not_needed_undone` |  | `RetroDecisions.UndoNotNeeded` |
| `retro.merge_kept_apart` |  | `RetroMergeProposals.KeepApart` |
| `retro.actions_decided` | `decide_retro_actions` |  |
| `import.started` |  | `ImportJobs.Start` |
| `import.job_changed` |  | `ImportJobs.Rerun`, `ImportJobs.Retry`, `ImportJobs.Skip`, `ImportJobs.Stop` |
| `import.jira_connected` |  | `Jira.Put` |
| `import.completed` |  | system: ImportRunner |
| `import.failed` |  | system: ImportRunner |
| `webhook.received` |  | system: PullRequestWebhooks |
| `profile.updated` |  | `Profile.Update` |
| `profile.photo_changed` |  | `Profile.ReplacePhoto`, `Profile.RemovePhoto` |
| `instance.settings_changed` |  | `Instance.PutSettings` |
| `instance.integrations_changed` |  | `Integrations.Put` |
| `instance.analytics_changed` |  | `Analytics.Put` |
| `instance.agent_connection_created` |  | `AgentConnections.Create` |
| `instance.agent_connection_changed` |  | `AgentConnections.Update`, `AgentConnections.Archive`, `AgentConnections.RegenerateSecret`, `AgentConnections.RegenerateToken` |
| `instance.user_added` |  | `UsersAdmin.Add` |
| `instance.user_changed` |  | `UsersAdmin.ChangeEmail`, `UsersAdmin.IssueOneTimePassword`, `UsersAdmin.SetDisabled`, `UsersAdmin.SetInstanceAdmin`, `UsersAdmin.Unlock` |
| `instance.search_reindexed` |  | `SearchAdmin.Reindex` |
| `screen.viewed` |  | self-emitted: Usage.Screen |

`analytics.test` is not in the catalogue: the test send goes straight to the sink.
