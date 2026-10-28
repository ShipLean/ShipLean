// Every screen's address, in one place. Pages, the shell, links and the real-time client all go through this file, so
// a path changes here and nowhere else.
//
// The scheme:
//   Install level (no project; the crumbs start at "ShipLean"):
//     /login                      sign in            ?returnUrl=<path> is where to go afterwards
//     /projects/new               new project
//     /instance/:tab              instance settings  tab: general | users | projects | integrations | agents
//     /profile                    your profile
//     /import                     Jira import with no project yet (the stepper creates or picks one)
//     /authorize                  connect an agent  ?request=<id> is the pending authorize request
//   Project level (everything under /p/:key, the project key; the crumbs start at the key):
//     /p/:key/backlogs/stories    ?mode=list|planning|tree (list when absent)
//     /p/:key/backlogs/features   ?mode=            (the Features level)
//     /p/:key/backlogs/epics      ?mode=            (the Epics level)
//     /p/:key/items/:itemKey      one work item, itemKey like SHIP-141
//     /p/:key/items/:itemKey/attachments/:attachmentId   one attachment of that item, opened on its own page
//     /p/:key/boards/stories      ?settings=        (the board settings dialog's section, closed when absent)
//     /p/:key/boards/features     ?settings=
//     /p/:key/boards/epics        ?settings=
//     /p/:key/sprints             all sprints
//     /p/:key/sprints/:sprintId/backlog | capacity
//     /p/:key/sprints/:sprintId/taskboard   ?settings=   (the Taskboard settings dialog's section)
//     /p/:key/import              Jira import
//     /p/:key/settings/:tab       tab: general | states | members | sprints | integrations
//   The three backlogs also take ?new=epic|feature|story|bug: the new-item row (or dialog) open with that type, see newItemPath.
//   Every project-level route also takes ?item=<itemKey>: that work item open in a modal over the screen.
//
// A route id is also the screen id presence is reported by. Ids and paths are stable: later work mounts pages on
// them and does not rename them.

export const backlogModes = ['list', 'planning', 'tree'] as const;
export type BacklogMode = (typeof backlogModes)[number];

// The backlog level a backlog or board shows.
export type Level = 'stories' | 'features' | 'epics';

// The types a Create action can start; tasks are made under a parent, so they have no link.
export const NEW_ITEM_KINDS = ['epic', 'feature', 'story', 'bug'] as const;
export type NewItemKind = (typeof NEW_ITEM_KINDS)[number];

export const projectSettingsTabs = [
  'general',
  'states',
  'members',
  'sprints',
  'integrations',
] as const;
export type ProjectSettingsTab = (typeof projectSettingsTabs)[number];

export const instanceSettingsTabs = [
  'general',
  'users',
  'projects',
  'integrations',
  'agents',
] as const;
export type InstanceSettingsTab = (typeof instanceSettingsTabs)[number];

interface ProjectParams {
  key: string;
}
interface SprintParams extends ProjectParams {
  sprintId: string;
}

export interface RouteParams {
  login: Record<string, never>;
  newProject: Record<string, never>;
  instanceSettings: { tab: InstanceSettingsTab };
  profile: Record<string, never>;
  importStart: Record<string, never>;
  storiesBacklog: ProjectParams;
  featuresBacklog: ProjectParams;
  epicsBacklog: ProjectParams;
  workItem: ProjectParams & { itemKey: string };
  workItemAttachment: ProjectParams & { itemKey: string; attachmentId: string };
  storiesBoard: ProjectParams;
  featuresBoard: ProjectParams;
  epicsBoard: ProjectParams;
  sprints: ProjectParams;
  sprintBacklog: SprintParams;
  taskboard: SprintParams;
  capacity: SprintParams;
  jiraImport: ProjectParams;
  projectSettings: ProjectParams & { tab: ProjectSettingsTab };
  authorize: Record<string, never>;
}

export type RouteId = keyof RouteParams;

// The query string a route understands; anything else is dropped on build and parse.
export interface RouteQuery {
  mode?: BacklogMode;
  settings?: string;
  returnUrl?: string;
  request?: string;
  // A work item open in a modal over a project page (its key, like SHIP-12). Only project-level routes carry it.
  item?: string;
  // The backlogs open their new-item row (or dialog) with this type, then drop it from the address.
  new?: NewItemKind;
}

interface RouteDef<I extends RouteId> {
  // Segments of the path; ":name" is a parameter.
  pattern: string;
  query: readonly (keyof RouteQuery)[];
  // True for pages that belong to the install or to you: crumbs start at "ShipLean" and there is no Live dot.
  installLevel: boolean;
  // The crumbs after the project key (or "ShipLean").
  crumbs: (params: RouteParams[I], query: RouteQuery) => string[];
}

// The planning mode is its own crumb, as the design draws it; an open board settings dialog adds Settings.
const withMode = (crumbs: string[], query: RouteQuery) =>
  query.mode === 'planning' ? [...crumbs, 'Planning'] : crumbs;
const withSettings = (crumbs: string[], query: RouteQuery) =>
  query.settings ? [...crumbs, 'Settings'] : crumbs;

const routeDefs: { [I in RouteId]: RouteDef<I> } = {
  login: { pattern: '/login', query: ['returnUrl'], installLevel: true, crumbs: () => ['Sign in'] },
  newProject: {
    pattern: '/projects/new',
    query: [],
    installLevel: true,
    crumbs: () => ['New project'],
  },
  instanceSettings: {
    pattern: '/instance/:tab',
    query: [],
    installLevel: true,
    crumbs: () => ['Instance settings'],
  },
  profile: { pattern: '/profile', query: [], installLevel: true, crumbs: () => ['Your profile'] },
  importStart: {
    pattern: '/import',
    query: [],
    installLevel: true,
    crumbs: () => ['Jira import'],
  },
  storiesBacklog: {
    pattern: '/p/:key/backlogs/stories',
    query: ['mode', 'item', 'new'],
    installLevel: false,
    crumbs: (_p, q) => withMode(['Backlogs', 'Stories'], q),
  },
  featuresBacklog: {
    pattern: '/p/:key/backlogs/features',
    query: ['mode', 'item', 'new'],
    installLevel: false,
    crumbs: (_p, q) => withMode(['Backlogs', 'Features'], q),
  },
  epicsBacklog: {
    pattern: '/p/:key/backlogs/epics',
    query: ['mode', 'item', 'new'],
    installLevel: false,
    crumbs: (_p, q) => withMode(['Backlogs', 'Epics'], q),
  },
  workItem: {
    pattern: '/p/:key/items/:itemKey',
    query: ['item'],
    installLevel: false,
    crumbs: (p) => ['Backlogs', p.itemKey],
  },
  // The attachment crumb says "Attachment" until the page that knows the file's name replaces it.
  workItemAttachment: {
    pattern: '/p/:key/items/:itemKey/attachments/:attachmentId',
    query: ['item'],
    installLevel: false,
    crumbs: (p) => ['Backlogs', p.itemKey, 'Attachment'],
  },
  storiesBoard: {
    pattern: '/p/:key/boards/stories',
    query: ['settings', 'item'],
    installLevel: false,
    crumbs: (_p, q) => withSettings(['Boards', 'Stories'], q),
  },
  featuresBoard: {
    pattern: '/p/:key/boards/features',
    query: ['settings', 'item'],
    installLevel: false,
    crumbs: (_p, q) => withSettings(['Boards', 'Features'], q),
  },
  epicsBoard: {
    pattern: '/p/:key/boards/epics',
    query: ['settings', 'item'],
    installLevel: false,
    crumbs: (_p, q) => withSettings(['Boards', 'Epics'], q),
  },
  sprints: {
    pattern: '/p/:key/sprints',
    query: ['item'],
    installLevel: false,
    crumbs: () => ['Sprints', 'All sprints'],
  },
  // The sprint crumb says "Sprint" until the page that knows the sprint's name replaces it.
  sprintBacklog: {
    pattern: '/p/:key/sprints/:sprintId/backlog',
    query: ['item'],
    installLevel: false,
    crumbs: () => ['Sprints', 'Sprint', 'Backlog'],
  },
  taskboard: {
    pattern: '/p/:key/sprints/:sprintId/taskboard',
    query: ['settings', 'item'],
    installLevel: false,
    crumbs: (_p, q) => withSettings(['Sprints', 'Sprint', 'Taskboard'], q),
  },
  capacity: {
    pattern: '/p/:key/sprints/:sprintId/capacity',
    query: ['item'],
    installLevel: false,
    crumbs: () => ['Sprints', 'Sprint', 'Capacity'],
  },
  jiraImport: {
    pattern: '/p/:key/import',
    query: ['item'],
    installLevel: false,
    crumbs: () => ['Jira import'],
  },
  projectSettings: {
    pattern: '/p/:key/settings/:tab',
    query: ['item'],
    installLevel: false,
    crumbs: () => ['Settings'],
  },
  authorize: {
    pattern: '/authorize',
    query: ['request'],
    installLevel: true,
    crumbs: () => ['Connect an agent'],
  },
};

export const routeIds = Object.keys(routeDefs) as RouteId[];

type AnyDef = RouteDef<RouteId> & { crumbs: (params: never, query: RouteQuery) => string[] };
const defOf = (id: RouteId) => routeDefs[id] as AnyDef;

// The router pattern of a route ("/p/:key/sprints/:sprintId/taskboard"), for the route table.
export function routePattern(id: RouteId): string {
  return routeDefs[id].pattern;
}

export function routePath<I extends RouteId>(
  id: I,
  params: RouteParams[I],
  query: RouteQuery = {},
): string {
  const values = params as Record<string, string>;
  const def = defOf(id);
  const path = def.pattern
    .split('/')
    .map((segment) =>
      segment.startsWith(':') ? encodeURIComponent(values[segment.slice(1)]) : segment,
    )
    .join('/');
  const search = new URLSearchParams();
  for (const name of def.query) {
    const value = query[name];
    if (value !== undefined && !(name === 'item' && value === '')) search.set(name, value);
  }
  const text = search.toString();
  return text === '' ? path : `${path}?${text}`;
}

export interface MatchedRoute<I extends RouteId = RouteId> {
  id: I;
  params: RouteParams[I];
  query: RouteQuery;
}

function matchPattern(pattern: string, pathname: string): Record<string, string> | undefined {
  const want = pattern.split('/');
  const have = pathname.replace(/\/+$/, '').split('/');
  if (want.length !== have.length) return undefined;
  const params: Record<string, string> = {};
  for (let i = 0; i < want.length; i++) {
    if (want[i].startsWith(':')) {
      if (have[i] === '') return undefined;
      try {
        params[want[i].slice(1)] = decodeURIComponent(have[i]);
      } catch {
        return undefined;
      }
    } else if (want[i] !== have[i]) return undefined;
  }
  return params;
}

const oneOf = <T extends string>(values: readonly T[], value: string | undefined) =>
  values.find((v) => v === value);

function parseQuery(def: AnyDef, search: string): RouteQuery {
  const raw = new URLSearchParams(search);
  const query: RouteQuery = {};
  for (const name of def.query) {
    const value = raw.get(name);
    if (value === null || (name === 'item' && value === '')) continue;
    if (name === 'mode') {
      const mode = oneOf(backlogModes, value);
      if (mode !== undefined) query.mode = mode;
    } else if (name === 'new') {
      const kind = oneOf(NEW_ITEM_KINDS, value);
      if (kind !== undefined) query.new = kind;
    } else query[name] = value;
  }
  return query;
}

// Which screen an address is. Undefined when it is none of them (or a tab the screen doesn't have).
export function matchRoute(pathname: string, search = ''): MatchedRoute | undefined {
  for (const id of routeIds) {
    const def = defOf(id);
    const params = matchPattern(def.pattern, pathname);
    if (params === undefined) continue;
    if ('tab' in params) {
      const tabs: readonly string[] =
        id === 'instanceSettings' ? instanceSettingsTabs : projectSettingsTabs;
      if (!tabs.includes(params.tab)) return undefined;
    }
    return { id, params, query: parseQuery(def, search) } as MatchedRoute;
  }
  return undefined;
}

export function isInstanceLevel(id: RouteId): boolean {
  return routeDefs[id].installLevel;
}

// The breadcrumb trail: the project key (or "ShipLean" for install-level pages), then the screen's crumbs.
export function crumbTrail<I extends RouteId>(
  id: I,
  params: RouteParams[I],
  query: RouteQuery = {},
): string[] {
  const def = routeDefs[id] as unknown as RouteDef<I>;
  const root = def.installLevel ? 'ShipLean' : (params as ProjectParams).key;
  return [root, ...def.crumbs(params, query)];
}

const NEW_ITEM_BACKLOG = {
  epic: 'epicsBacklog',
  feature: 'featuresBacklog',
  story: 'storiesBacklog',
  bug: 'storiesBacklog',
} as const satisfies Record<NewItemKind, RouteId>;

// The backlog that creates an item of this kind: the Create actions in quick search link here.
export function newItemPath(key: string, kind: NewItemKind): string {
  return routePath(NEW_ITEM_BACKLOG[kind], { key }, { new: kind });
}
