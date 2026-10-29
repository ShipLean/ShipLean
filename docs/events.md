# Project events

Every write that other people should see live publishes a `ProjectEvent` on `IProjectEvents` (`ShipLean.Contracts.Events`). The real-time hub relays them to the project's SignalR group and the client invalidates queries from them (`registerInvalidation(eventType, handler)` keys on the `type` strings below). Events carry ids only, never whole DTOs: a client refetches what it shows.

## The bus

- `IProjectEvents.PublishAsync(event)` only enqueues. Delivery happens off the publishing request, in publish order per subscriber; `Subscribe(projectId, handler)` (`null` = every project) returns an `IDisposable` that stops delivery. A handler that throws is logged and the subscription carries on.
- Publishers call it **after** their transaction commits, never inside it, so a subscriber that refetches sees the change.
- `InMemoryProjectEvents` is the production bus (one process). It is a singleton, so it reads the `X-ShipLean-Client` header of the current request itself on every publish (through `IHttpContextAccessor`) and stamps it as `clientId` (blank or no request = `null`). A tab ignores events carrying its own client id.
- The in-memory fakes and contract fixtures publish no events; event assertions belong to the real stores' own tests.
- The bus itself has no cross-process bridge: an event is relayed by the instance that published it, and the SignalR Redis backplane carries the relay's group send to every instance (see [Running more than one instance](#running-more-than-one-instance)).
- Stamping uses the request the publisher runs in. **Background work** (the import runner) must start under `ExecutionContext.SuppressFlow()` or from a hosted queue, so its events carry `clientId: null` rather than the id of the tab whose request started it; otherwise that tab would filter out its own import's `importProgress`.
- The client's own-`clientId` filter skips `importProgress` and `presenceChanged`: every tab, including the one that caused them, needs those.
- **Relaying (the hub):** serialise as the base type, `JsonSerializer.SerializeToElement<ProjectEvent>(e, options)`, with the API's options (camelCase string enums). Serialising by runtime type (what SignalR does with the event object) drops `type`, and `JsonSerializerOptions.Web` alone writes enums as numbers.
- Each subscription has an unbounded queue: subscribe once per relay, never once per connection.
- **Order keys are not patched in place.** A write may renumber its neighbours' order keys silently (a rebalance when keys grow long, or when a tie is repaired), and publishes only the event of the card it changed. On `workItemCreated`, `workItemMoved` and `workItemReordered` a client refetches the list, column or cell it shows rather than writing the one `backlogOrder` or `columnOrder` from the event into its cache.

## Wire format

JSON objects, camelCase, enums as camelCase strings, nothing omitted. Every event has `type`, `projectId` and `clientId` (string or `null`), plus:

| `type` | Fields | Published when |
|---|---|---|
| `workItemCreated` | `id`, `key`, `workItemType`, `parentId` | a work item is created |
| `workItemUpdated` | `id`, `revision`, `fields` (field ids), `pulled` | a patch is saved; `pulled` when a rule amended it with a "pulled" notice |
| `workItemMoved` | `id`, `revision`, `columnId`, `done`, `laneId`, `columnOrder`, `stateId`, `parentId` | a card is moved on a board or the taskboard (`columnId` is `null` on the taskboard, where `columnOrder` orders the card within its state and parent cell; `columnOrder` is `null` when the card left its board) |
| `workItemReordered` | `id`, `backlogOrder` | a backlog reorder |
| `workItemRemoved` | `id`, `parentId` | a work item enters the Removed state |
| `boardSettingsChanged` | `boardId`, `sections` | board settings are saved (`sections` = top-level properties changed: `columns`, `swimlanes`, `styleRules`, `cardFields`, `tagColours`, `annotations`, `cardReordering`, `transitions`), or a project-states Save repaired a board (state mappings of `columns`, and `transitions` re-bound by state name), with `sections` listing what changed |
| `sprintChanged` | `sprintId` | a sprint, its capacity or its rollover changes |
| `statesChanged` | none | the project's states change |
| `projectChanged` | none | project settings, members or the forecast change |
| `presenceChanged` | `screenId` (route id), `userIds`, `sequence` | the people on a screen change (`sequence` grows with every change: a client keeps the list with the highest one per screen) |
| `importProgress` | `jobId`, `percent`, `state`, `message` | an import job reports progress |
| `commentsChanged` | `workItemId` | a comment on the work item is added, edited or archived |
| `attachmentsChanged` | `workItemId` | an attachment on the work item is added or archived |
| `linksChanged` | `workItemId` | a link on the work item is added, edited or archived, or a link card's state or preview is refreshed |
| `agentSessionChanged` | `workItemId`, `sessionId` | an agent session on the work item opens, changes state or gets an activity, a link or a plan, or a prompt (a comment, Stop) is queued for it (`AgentSessionNotifier`; background writers publish with `clientId: null`) |

Pinned by `src/ShipLean.TestSupport/DtoFixtures/events/events.json` (one example per type), C# types in `src/ShipLean.Contracts/Events/`, TypeScript twin in `src/ShipLean.Client/src/api/events.ts`.

## The hub

`ProjectHub` at `/hubs/project` (SignalR, `src/ShipLean.WebApi/Realtime/`) carries the bus to browsers. Events only; there is no co-editing. **WebSockets only**: negotiate offers no other transport, so a client must not rely on a fallback.

**Server methods** (arguments are positional):

| Method | Does |
|---|---|
| `JoinProject(Guid projectId)` | requires `Member` on the project (the same rule as `IProjectAccess`, for the connection's own user), then adds the connection to the group `project:{projectId}`; membership is checked again after the add, so a removal that raced the join leaves the connection out. Joining twice is harmless |
| `LeaveProject(Guid projectId)` | leaves the group and the connection's screen in that project |
| `JoinScreen(Guid projectId, string screenId)` | requires `Member`, then records that this connection's user is on that screen and publishes `presenceChanged`. `screenId` must match `^[a-zA-Z]{1,40}$` (a route id), else `validation`. A connection is on **one screen per project**: joining a second leaves the first |
| `LeaveScreen(Guid projectId, string screenId)` | removes it and publishes `presenceChanged` |

**Client method:** `projectEvent(event)`, the `ProjectEvent` JSON of the wire-format table above. A connection receives nothing for a project it has not joined.

### Running more than one instance

The bus is in process, so an event is relayed by the instance that handled the write. With `SHIPLEAN_REDIS_CONNECTION` set, SignalR uses the Redis backplane (`Microsoft.AspNetCore.SignalR.StackExchangeRedis`), so that relay's group send reaches the group's connections on every instance. The load balancer still needs sticky sessions: negotiate and the WebSocket must land on the same instance. Every channel and key ShipLean uses on that Redis starts with one prefix: the connection string's `channelPrefix`, else `shiplean`. Two deployments sharing a Redis need different prefixes. Redis must be reachable when an instance starts: otherwise the start fails after a few seconds with `RedisConnectionException` (the member-removal subscription), and the container restarts.

The same Redis carries the two pieces of realtime state that are not events (`RealtimeServices` picks the Redis or in-process versions, as it picks the lifetime manager):

- **Presence** (`RedisPresenceTracker`). Each screen is a hash, `<prefix>:presence:<projectId>:<screenId>`, of connection id → `"<userId> <instanceId>"`. One Lua script applies a change, takes the next number from `<prefix>:presence-sequence` and returns the distinct user ids, atomically across instances. The instance that made the change publishes `presenceChanged` on its own bus. Two instances' lists can reach a client out of order, so the client drops a list whose `sequence` is not above the last one it kept for that screen (and forgets the sequences when its connection resets). `<prefix>:presence-screens` lists the screens with anyone on them.
- **Crashed instances** (`PresenceHeartbeat`). Every instance refreshes `<prefix>:presence-alive:<instanceId>` every 15 s with a 45 s TTL, and lists itself in `<prefix>:presence-instances`. On each beat it takes the entries of every listed instance whose heartbeat has expired off their screens, publishes the new lists, and unlists that instance. A killed instance's users drop off within about a minute. A clean stop deletes the heartbeat, so the others clean up at their next beat. An instance that loses Redis for longer than the TTL is cleaned up too, and its users show again only when they next join a screen.
- **Removing a member** (`IMemberRemovals`). After a removal commits, `RedisMemberRemovals` drops the user's connections on its own instance, then publishes `{ projectId, userId }` on `<prefix>:members-removed`. Every instance handles that by calling its own `ProjectGroupMembers.RemoveUserAsync`. Redis pub/sub is at most once, so `HubSessionSweep` is the backstop (below). A failed publish is logged, not raised: the removal has already committed.
- **The session sweep.** Each instance ends its own connections whose session has ended, which is all it needs to do.

Without `SHIPLEAN_REDIS_CONNECTION` the in-process `PresenceTracker` and `LocalMemberRemovals` are used. They keep the same contract, including `sequence`.

Tests: `MultiInstanceRealtimeTests` runs two hosts on one database and one Redis (`RedisServer`: `SHIPLEAN_TEST_REDIS`, else a `redis:7.4` Testcontainers container), each test class under a channel prefix of its own.

**Failures** are a `HubException` carrying `<code>: <message>`, but SignalR wraps it, so the client's error message is, exactly:

`An unexpected error occurred invoking 'JoinProject' on the server. HubException: not_found: That project or item was not found.`

It does **not** start with the code. A client reads the code with `/HubException: ([a-z_]+): /` (group 1). A non-member gets `not_found`, exactly like an unknown id. Codes a client can see from the hub: `not_found`, `validation`, `session_ended`, `password_change_required`.

**Order and untrusted text.** The client must `JoinProject` before `JoinScreen`: the presence list goes to the project's group, so a connection that joins a screen first never receives the list of who is already there. Strings in events (`screenId`, `clientId`, labels) are untrusted text: render them as text only, and match `screenId` against known screens.

**The relay.** `ProjectEventsRelay` (hosted service) holds the one bus subscription for all projects and sends each event to its project's group, serialised as `ProjectEvent` with the API's JSON options (see Relaying above).

**Presence.** `IPresenceTracker` keeps, per connection, who is on which screen. Every join, leave and disconnect publishes `presenceChanged { screenId, userIds, sequence }` with everyone now on that screen (distinct, so two tabs of one user list them once; empty when the last one leaves). It is a bus event like any other: the relay delivers it to the project's group, and the client does not filter it by `clientId`. A closed connection leaves every screen it was on, and so does `LeaveProject` for its project and `RemoveUserAsync` for the removed user (the new lists are published). In process (`PresenceTracker`), one lock per (project, screen) holds the change, the snapshot and the publish together, so the last `presenceChanged` for a screen always lists everyone on it. With Redis, the script does that across instances and `sequence` puts the lists back in order on the client.

**Dropping a removed member.** `ProjectSettingsController` calls `IMemberRemovals.RemovedAsync(projectId, userId)` after a member is removed and after a member leaves, once the change has committed. On every instance, it takes all of that user's connections out of the project's group and off its screens (`ProjectGroupMembers.RemoveUserAsync`). `JoinProject` and `JoinScreen` re-check access on every call, so a removed member cannot rejoin. As a backstop, `HubSessionSweep` also re-checks, once a minute, the membership of every (project, user) a connection on its instance joined (`HubMembership.NotMembersAsync`, the same rule as `RequireMemberAsync`), and drops the users who are no longer members. A removal that never arrived therefore lasts at most about a minute.

**Security.** The hub is `[Authorize]` and its endpoint also requires `ProjectHubRequirement`, so both negotiate and the WebSocket connect are checked:

- *Session.* The cookie handler ends a session whose security stamp changed or whose account is disabled, with 401 `session_ended`, as for `/api`. A user on a one-time password gets 403 `password_change_required`. An anonymous caller gets 401. A session that ends while the socket is open (disabled, security stamp changed, one-time password set) is cut off: a hub filter re-checks it on every call (with the freshness controllers have, `UserSessions`, at most a minute old), refusing with `session_ended` and aborting the connection or refusing with `password_change_required`, and a hosted sweep (`HubSessionSweep`, once a minute, reading the same cache) aborts such connections that stay silent. So a socket that makes no calls drops within about two minutes (the cache's minute plus the sweep's). The client never sees `session_ended` itself: it sees a closed socket (the abort overtakes the reply), and its reconnect then gets 401 `session_ended`. A database error during a sweep is logged and that connection is left open until the next round; it never stops the app. A cookie identity without a stamp claim is refused, never waved through. `CloseOnAuthenticationExpiration` ends a socket when its cookie expires.
- *User.* The hub reads the user from `Context.User` (and the filter too), never from `IHttpContextAccessor` or `ICurrentUser`, which SignalR does not guarantee inside a hub.
- *Origin* (`CrossSiteWriteGuard` covers only `/api`, and a browser WebSocket carries the cookie from any site). An absent `Origin` header is allowed (non-browser clients). A present one must be exactly the request's own scheme, host and port after forwarded headers, with no path or user info; otherwise 403 `forbidden`.
- *Access.* Every `JoinProject` and `JoinScreen` checks membership with `HubMembership`, which takes the connection's own user (`IProjectAccess` reads the request's user, which a hub does not have) and applies `ProjectAccess`'s rule (a live `project_member` row of any role, else the same `not_found`); a parity test keeps the two in step. `JoinScreen` re-checks after adding the presence entry, as `JoinProject` does after the group add, and leaves again if the check no longer holds.

Tests: `src/ShipLean.WebApi.Tests/Realtime/` (`ProjectHubTests`, `ProjectHubSecurityTests`, `HubMembershipSweepTests`, `MultiInstanceRealtimeTests`).
