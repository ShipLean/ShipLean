# Navigation: routes, pages and slots

The address of every screen is in `src/ShipLean.Client/src/routes.ts` (ids, paths, query, crumbs). `AppRoutes.tsx` is the route table; it builds each path from `routePattern(id)`, so a path changes in `routes.ts` and nowhere else. Link with `routePath(id, params, query)`, never a hand-written string.

## Routes

`/login` is outside the shell. Everything else renders inside `<Shell />` (top bar, side menu) with the page in its outlet. `/` is `RootRedirect`.

| Level | Route id | Path | Query |
|---|---|---|---|
| Install | `newProject` | `/projects/new` | |
| Install | `instanceSettings` | `/instance/:tab` (general, users, projects) | |
| Install | `profile` | `/profile` | |
| Install | `importStart` | `/import` | |
| Install | `authorize` | `/authorize` | `request` |
| Project | `storiesBacklog`, `featuresBacklog`, `epicsBacklog` | `/p/:key/backlogs/{stories,features,epics}` | `mode` (list, planning, tree) |
| Project | `workItem` | `/p/:key/items/:itemKey` | |
| Project | `workItemAttachment` | `/p/:key/items/:itemKey/attachments/:attachmentId` | |
| Project | `storiesBoard`, `featuresBoard`, `epicsBoard` | `/p/:key/boards/{stories,features,epics}` | `settings` |
| Project | `sprints` | `/p/:key/sprints` | |
| Project | `sprintBacklog`, `capacity` | `/p/:key/sprints/:sprintId/{backlog,capacity}` | |
| Project | `taskboard` | `/p/:key/sprints/:sprintId/taskboard` | `settings` |
| Project | `jiraImport` | `/p/:key/import` | |
| Project | `projectSettings` | `/p/:key/settings/:tab` (general, states, members, sprints, integrations) | |

An unknown address (under `/p/:key` or anywhere else, such as `/nowhere`), an unknown or inaccessible project key, a sprint id the project does not have and a tab that does not exist all show the not-found state (`pagesTestIds.notFound`).

## Pages

`src/pages/<Screen>Page.tsx`. A page resolves what the URL names, reports presence (`useScreenPresence(routeId)`) and lays out slots. It fetches only the project (`['projects', 'by-key', key]`) and, where a slot needs them, the sprints (`['projects', projectId, 'sprints']`); every other read belongs to a slot. A page holds no screen content.

- A page's slot wiring is a pure function beside it (`backlogSlots`, `boardSlots`, `workItemSlots`, `sprintsSlots`, `sprintBacklogSlots`, `taskboardSlots`, `capacitySlots`, `importSlots`, `projectSettingsSlots`, `instanceSettingsSlots`; `pageRouteIds` has the presence ids). They return each slot's props (or `undefined` for a slot not mounted), and `pages/pageWiring.test.tsx` pins them. Filling a slot never means editing them.
- Data: the page renders its slots whenever it holds data. The not-found and load-failed states appear only while there is none, so a failed refetch never unmounts a slot. If the sprints cannot be read, a backlog page still renders and its sprint end prompt gets `sprints: []`.
- Retries: one policy for every query, the `QueryClient` default in `main.tsx` (`queries/retry.ts`): a 4xx answer is final, a network error or 5xx is retried up to three times. Do not set `retry` on a query.
- Router: `main.tsx` uses a data router (`createBrowserRouter([{ path: '*', element: <AppRoutes /> }])`) and the component test harness the same shape with `createMemoryRouter`, so a screen can use `useBlocker`.
- `ProjectScreen` (project page root: presence, project, not found), `SprintScreen` (also the sprint), `InstallScreen` (install-level root), `WithSprints` (reads the sprints for the part of a page that needs them).
- Sprint pages render `SprintHeader` above their slots: `full` on the Sprint backlog and Capacity, `slim` on the Taskboard. `SprintHeader` renders `sprintGoalEdit`; the page does not.
- `?settings=` on the boards and the Taskboard: the page reads it as `section`; `onSectionChange(section)` sets it (replacing the history entry), `onClose()` removes it. The slot renders its dialog only while `section` is set. To open a dialog from elsewhere, link to `?settings=<section>` (for example `columns` or `fields`).
- `sprintGoalEdit`: the slot calls `onDone()` after the goal PATCH succeeds, having invalidated `['projects', projectId, 'sprints']`, and on Cancel or Escape. The header closes the editor and returns focus to its Edit button; the slot moves focus into its input when it mounts.
- Backlogs: Stories show `listMode`, `planningMode` or `treeMode` by `?mode=` (list when absent or invalid); Features and Epics show `levelBacklog`. `sprintEndPrompt` is mounted on every backlog page and on the Sprints page.
- Project settings: `settingsDraft` is the frame of **every** tab (tab list with dirty dots, the one Save and Discard header, Save jumping to the first error). The page passes it the content of the States and Integrations tabs as `states` and `integrations` (elements of `settingsStates` and `settingsIntegrations`), and the frame renders them on those tabs. The frame shares the draft with `settingsStates` through its own context.
- Work item: `form` is the page frame (header and fields strip across the full width, then the main column beside the rail, as `work-item.html` draws it). The page passes it `links`, `tasks`, `discussion` and `side` as elements; the frame puts Links at the top of the rail, then Side, and Tasks and Discussion in the main column.
- Instance settings: each tab mounts its own slot, with the `tab`.

## Slots

`src/slots/<SlotName>Slot.tsx` exports `<SlotName>Slot` and `<SlotName>SlotProps`. One file per slot; a slot mounts its feature and the page is not edited to change what fills it. Each slot's root carries the `data-testid` from `pagesTestIds.slot<SlotName>` (`slot-<slotName>`); keep that id on the root so page-level tests still find it. `Level` is `'stories' | 'features' | 'epics'`.

The five work item slots read the item through the query key `['projects', projectId, 'work-items', 'by-key', itemKey]`.

| Slot | Page | Props |
|---|---|---|
| `listMode` | Backlogs (stories, list) | `{ project, level, mode }` |
| `planningMode` | Backlogs (stories, planning) | `{ project, level, mode }` |
| `treeMode` | Backlogs (stories, tree) | `{ project, level, mode }` |
| `levelBacklog` | Backlogs (features, epics) | `{ project, level, mode }` |
| `sprintEndPrompt` | Backlogs, Sprints | `{ project, sprints }` |
| `form` | Work item | `{ project, itemKey, links, tasks, discussion, side: ReactNode }` (the frame) |
| `links`, `tasks`, `discussion`, `side` | Work item, inside `form` | `{ project, itemKey }` |
| `attachment` | Attachment | `{ project, itemKey, attachmentId }` |
| `board` | Boards | `{ project, level }` |
| `boardSettings` | Boards | `{ project, level, section: string \| undefined, onSectionChange(section), onClose() }`; the slot renders `BoardSettingsDialog({ projectId: project.id, level, section, onSectionChange, onClose })`, which finds the board's id by level itself (through `['projects', projectId, 'boards']`) |
| `taskboard` | Taskboard | `{ project, sprint, sprints }` |
| `taskboardSettings` | Taskboard | `{ project, sprint, sprints, section, onSectionChange, onClose }` |
| `sprintsList`, `newSprintRail` | Sprints | `{ project, sprints }` |
| `capacity` | Capacity | `{ project, sprint, sprints }` |
| `sprintBacklog` | Sprint backlog | `{ project, sprint, sprints }` |
| `sprintGoalEdit` | rendered by `SprintHeader` | `{ project, sprint, sprints, onDone() }` |
| `settingsDraft` | Project settings (every tab; the frame) | `{ project, tab, states, integrations: ReactNode }` |
| `settingsStates` | Project settings, inside `settingsDraft` | `{ project, tab }` |
| `settingsIntegrations` | Project settings, inside `settingsDraft` | `{ project, tab }` |
| `importStepper`, `importRun` | Jira import (both routes) | `{ project: ProjectDto \| null }` (null at `/import`) |
| `newProject` | New project | `{}` |
| `instanceGeneral`, `instanceUsers`, `instanceProjects` | Instance settings | `{ tab }` |
| `profileIdentity`, `profileAccount`, `profileConnectedAgents` | Your profile | `{}` |

`project` is a `ProjectDto`, `sprint` and `sprints` are `SprintDto` and `SprintDto[]`.

## The project picker

Picking a project in the top bar goes to the same screen in that project (`features/shell/sameScreenIn.ts`): the route id, its params and its query are kept, with the new key. Four screens belong to one project's sprint or item, so they fall back: the Sprint taskboard, the Sprint backlog and Capacity go to All sprints (`sprints`), and a work item goes to the Stories backlog. An install-level page (profile, instance settings, new project, import start, authorize) or an unknown address also goes to the Stories backlog.

## Testing

Pure wiring is tested with vitest; component tests assert only each slot's `slot-<name>` root, never a slot's own content. Component tests mount `AppRoutes` with `hooksConfig.route` and stub `/api/**` (`pages/pagesFixtures.ts` has `stubProjectApi`). The scaffold roots and slot ids are `pagesTestIds` (`features/pages/testIds.ts`, twin `PagesTestIds.cs`). The acceptance page object is `Pages/Routes/ScaffoldScreen.cs`.
