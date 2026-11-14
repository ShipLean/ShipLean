import { expect, test } from 'vitest';
import {
  crumbTrail,
  instanceSettingsTabs,
  isInstanceLevel,
  matchRoute,
  newItemPath,
  projectSettingsTabs,
  routeIds,
  routePath,
  type RouteId,
  type RouteParams,
  type RouteQuery,
} from './routes';

const project = { key: 'SHIP' };
const sprint = { key: 'SHIP', sprintId: 's 24' };

const samples: {
  [I in RouteId]: { params: RouteParams[I]; query?: RouteQuery; path: string; crumbs: string[] };
} = {
  login: {
    params: {},
    query: { returnUrl: '/p/SHIP/sprints?x=1' },
    path: '/login?returnUrl=%2Fp%2FSHIP%2Fsprints%3Fx%3D1',
    crumbs: ['ShipLean', 'Sign in'],
  },
  newProject: { params: {}, path: '/projects/new', crumbs: ['ShipLean', 'New project'] },
  instanceSettings: {
    params: { tab: 'users' },
    path: '/instance/users',
    crumbs: ['ShipLean', 'Instance settings'],
  },
  profile: { params: {}, path: '/profile', crumbs: ['ShipLean', 'Your profile'] },
  importStart: { params: {}, path: '/import', crumbs: ['ShipLean', 'Jira import'] },
  authorize: {
    params: {},
    query: { request: 'r-1' },
    path: '/authorize?request=r-1',
    crumbs: ['ShipLean', 'Connect an agent'],
  },
  mobileSignIn: {
    params: {},
    query: { challenge: 'c', state: 's', device: 'Pixel' },
    path: '/mobile/sign-in?challenge=c&state=s&device=Pixel',
    crumbs: ['ShipLean', 'Sign in to the app'],
  },
  storiesBacklog: {
    params: project,
    query: { mode: 'planning' },
    path: '/p/SHIP/backlogs/stories?mode=planning',
    crumbs: ['SHIP', 'Backlogs', 'Stories', 'Planning'],
  },
  featuresBacklog: {
    params: project,
    query: { mode: 'tree' },
    path: '/p/SHIP/backlogs/features?mode=tree',
    crumbs: ['SHIP', 'Backlogs', 'Features'],
  },
  epicsBacklog: {
    params: project,
    path: '/p/SHIP/backlogs/epics',
    crumbs: ['SHIP', 'Backlogs', 'Epics'],
  },
  workItem: {
    params: { ...project, itemKey: 'SHIP-141' },
    path: '/p/SHIP/items/SHIP-141',
    crumbs: ['SHIP', 'Backlogs', 'SHIP-141'],
  },
  workItemAttachment: {
    params: { ...project, itemKey: 'SHIP-101', attachmentId: 'a 1' },
    path: '/p/SHIP/items/SHIP-101/attachments/a%201',
    crumbs: ['SHIP', 'Backlogs', 'SHIP-101', 'Attachment'],
  },
  taskChanges: {
    params: { ...project, itemKey: 'SHIP-142' },
    query: { commit: 'c-1', file: 'src/a b.ts', layout: 'split' },
    path: '/p/SHIP/items/SHIP-142/changes?commit=c-1&file=src%2Fa+b.ts&layout=split',
    crumbs: ['SHIP', 'Backlogs', 'SHIP-142', 'Changes'],
  },
  featureBoard: {
    params: { key: 'SHIP', itemKey: 'SHIP-101' },
    query: { settings: 'fields' },
    path: '/p/SHIP/items/SHIP-101/board?settings=fields',
    crumbs: ['SHIP', 'Backlogs', 'SHIP-101', 'Feature board', 'Settings'],
  },
  storiesBoard: {
    params: project,
    query: { settings: 'columns' },
    path: '/p/SHIP/boards/stories?settings=columns',
    crumbs: ['SHIP', 'Boards', 'Stories', 'Settings'],
  },
  featuresBoard: {
    params: project,
    path: '/p/SHIP/boards/features',
    crumbs: ['SHIP', 'Boards', 'Features'],
  },
  epicsBoard: {
    params: project,
    path: '/p/SHIP/boards/epics',
    crumbs: ['SHIP', 'Boards', 'Epics'],
  },
  sprints: { params: project, path: '/p/SHIP/sprints', crumbs: ['SHIP', 'Sprints', 'All sprints'] },
  sprintBacklog: {
    params: sprint,
    path: '/p/SHIP/sprints/s%2024/backlog',
    crumbs: ['SHIP', 'Sprints', 'Sprint', 'Backlog'],
  },
  taskboard: {
    params: sprint,
    query: { settings: 'fields' },
    path: '/p/SHIP/sprints/s%2024/taskboard?settings=fields',
    crumbs: ['SHIP', 'Sprints', 'Sprint', 'Taskboard', 'Settings'],
  },
  capacity: {
    params: sprint,
    path: '/p/SHIP/sprints/s%2024/capacity',
    crumbs: ['SHIP', 'Sprints', 'Sprint', 'Capacity'],
  },
  sprintRetro: {
    params: sprint,
    path: '/p/SHIP/sprints/s%2024/retro',
    crumbs: ['SHIP', 'Sprints', 'Sprint', 'Retro'],
  },
  sprintDemo: {
    params: sprint,
    path: '/p/SHIP/sprints/s%2024/demo',
    crumbs: ['SHIP', 'Sprints', 'Sprint', 'Demo'],
  },
  retroFeatures: {
    params: project,
    path: '/p/SHIP/retros/features',
    crumbs: ['SHIP', 'Retrospectives', 'Features'],
  },
  retroItem: {
    params: { ...project, itemKey: 'SHIP-141' },
    query: { retro: 'r1', decide: 'open', point: 'p1' },
    path: '/p/SHIP/retros/items/SHIP-141?retro=r1&decide=open&point=p1',
    crumbs: ['SHIP', 'Retrospectives', 'Features', 'SHIP-141'],
  },
  retros: {
    params: project,
    query: {
      q: 'flaky',
      sprint: 's24',
      category: 'improve',
      recurring: 'true',
      selected: 'r1',
      noSprint: 'true',
      decision: 'undecided',
      unthemed: 'true',
    },
    path: '/p/SHIP/retros?q=flaky&sprint=s24&category=improve&recurring=true&selected=r1&noSprint=true&decision=undecided&unthemed=true',
    crumbs: ['SHIP', 'Retrospectives', 'Search'],
  },
  retroInsights: {
    params: project,
    query: { view: 'impact', range: 'w6' },
    path: '/p/SHIP/retros/insights?view=impact&range=w6',
    crumbs: ['SHIP', 'Retrospectives', 'Insights'],
  },
  jiraImport: { params: project, path: '/p/SHIP/import', crumbs: ['SHIP', 'Jira import'] },
  projectSettings: {
    params: { ...project, tab: 'states' },
    path: '/p/SHIP/settings/states',
    crumbs: ['SHIP', 'Settings'],
  },
};

test('routes_ShouldBuildEveryPathAndCrumb', () => {
  expect(Object.keys(samples).sort()).toEqual([...routeIds].sort());

  for (const id of routeIds) {
    const sample = samples[id];
    const path = routePath(id, sample.params as never, sample.query);
    expect(path, id).toBe(sample.path);

    const [pathname, search = ''] = path.split('?');
    expect(matchRoute(pathname, search ? `?${search}` : ''), id).toEqual({
      id,
      params: sample.params,
      query: sample.query ?? {},
    });
    expect(crumbTrail(id, sample.params as never, sample.query), id).toEqual(sample.crumbs);
  }
});

test('routes_WhenNothingMatches_ShouldReturnUndefined', () => {
  expect(matchRoute('/')).toBeUndefined();
  expect(matchRoute('/p/SHIP')).toBeUndefined();
  expect(matchRoute('/instance/nope')).toBeUndefined();
  expect(matchRoute('/p/SHIP/settings/nope')).toBeUndefined();
});

test('routes_WhenQueryIsUnknown_ShouldDropIt', () => {
  expect(matchRoute('/p/SHIP/backlogs/stories', '?mode=bogus&x=1')?.query).toEqual({});
  expect(matchRoute('/p/SHIP/backlogs/stories/', '?mode=tree')?.query).toEqual({ mode: 'tree' });
});

test('routes_ShouldFlagInstanceLevelPages', () => {
  expect(routeIds.filter(isInstanceLevel).sort()).toEqual([
    'authorize',
    'importStart',
    'instanceSettings',
    'login',
    'mobileSignIn',
    'newProject',
    'profile',
  ]);
});

test('routes_WhenModeAndSettingsAreAbsent_ShouldDropTheExtraCrumb', () => {
  expect(crumbTrail('storiesBacklog', project)).toEqual(['SHIP', 'Backlogs', 'Stories']);
  expect(crumbTrail('storiesBacklog', project, { mode: 'tree' })).toEqual([
    'SHIP',
    'Backlogs',
    'Stories',
  ]);
  expect(crumbTrail('epicsBoard', project)).toEqual(['SHIP', 'Boards', 'Epics']);
});

test('routes_WhenSettingsTabIsDemos_ShouldMatchItAfterIntegrations', () => {
  expect(matchRoute('/p/SHIP/settings/demos')?.id).toBe('projectSettings');
  expect(projectSettingsTabs.at(-1)).toBe('demos');
  expect(projectSettingsTabs.at(-2)).toBe('integrations');
});

test('routes_WhenInstanceTabIsAgents_ShouldMatchIt', () => {
  expect(matchRoute('/instance/agents')?.id).toBe('instanceSettings');
});

test('routes_WhenInstanceTabIsBackups_ShouldMatchItAfterAgents', () => {
  expect(matchRoute('/instance/backups')?.id).toBe('instanceSettings');
  expect(routePath('instanceSettings', { tab: 'backups' })).toBe('/instance/backups');
  expect(instanceSettingsTabs.at(-1)).toBe('backups');
  expect(instanceSettingsTabs.at(-2)).toBe('agents');
});

test('routes_WhenInstanceTabIsIntegrations_ShouldMatchIt', () => {
  expect(matchRoute('/instance/integrations')?.id).toBe('instanceSettings');
});

test('routes_WhenProjectRouteHasItem_ShouldRoundTripIt', () => {
  expect(routePath('storiesBoard', { key: 'SHIP' }, { item: 'SHIP-12' })).toBe(
    '/p/SHIP/boards/stories?item=SHIP-12',
  );
  expect(matchRoute('/p/SHIP/boards/stories', '?item=SHIP-12')?.query).toEqual({ item: 'SHIP-12' });
  expect(
    routePath('taskboard', { key: 'SHIP', sprintId: 's1' }, { settings: 'x', item: 'A-1' }),
  ).toBe('/p/SHIP/sprints/s1/taskboard?settings=x&item=A-1');
  expect(matchRoute('/p/SHIP/sprints/s1/taskboard', '?item=A-1')?.query).toEqual({ item: 'A-1' });
});

test('routes_WhenInstallRouteHasItem_ShouldDropIt', () => {
  expect(routePath('login', {}, { item: 'SHIP-12' })).toBe('/login');
  expect(matchRoute('/login', '?item=SHIP-12')?.query).toEqual({});
});

test('routes_WhenItemIsEmpty_ShouldDropIt', () => {
  expect(matchRoute('/p/SHIP/boards/stories', '?item=')?.query).toEqual({});
  expect(routePath('storiesBoard', { key: 'SHIP' }, { item: '' })).toBe('/p/SHIP/boards/stories');
});

test('newItemPath_WhenEachKind_ShouldBuildTheBacklogAddress', () => {
  expect(newItemPath('SHIP', 'epic')).toBe('/p/SHIP/backlogs/epics?new=epic');
  expect(newItemPath('SHIP', 'feature')).toBe('/p/SHIP/backlogs/features?new=feature');
  expect(newItemPath('SHIP', 'story')).toBe('/p/SHIP/backlogs/stories?new=story');
  expect(newItemPath('SHIP', 'bug')).toBe('/p/SHIP/backlogs/stories?new=bug');
  expect(newItemPath('A B', 'bug')).toBe('/p/A%20B/backlogs/stories?new=bug');
});

test('routes_WhenBacklogHasNew_ShouldStillMatchAndKeepIt', () => {
  for (const [path, kind] of [
    ['/p/SHIP/backlogs/stories', 'bug'],
    ['/p/SHIP/backlogs/features', 'feature'],
    ['/p/SHIP/backlogs/epics', 'epic'],
  ] as const) {
    const matched = matchRoute(path, `?new=${kind}&mode=tree`);
    expect(matched?.query, path).toEqual({ new: kind, mode: 'tree' });
  }
});

test('routes_WhenNewIsNotAKind_ShouldDropIt', () => {
  expect(matchRoute('/p/SHIP/backlogs/stories', '?new=task')?.query).toEqual({});
  expect(matchRoute('/p/SHIP/boards/stories', '?new=story')?.query).toEqual({});
});

test('routes_SprintRetro_ShouldKeepDecidePointViewAndWrittenBy', () => {
  const query = { decide: 'open', point: 'p1', view: 'item', writtenBy: 'agents' };
  const path = routePath('sprintRetro', sprint, query);
  expect(path).toBe('/p/SHIP/sprints/s%2024/retro?decide=open&point=p1&view=item&writtenBy=agents');
  const [pathname, search] = path.split('?');
  expect(matchRoute(pathname, `?${search}`)?.query).toEqual(query);
});
