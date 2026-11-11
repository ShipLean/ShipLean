// The only place a route path is written. Screens call router.push(links.x()) and never build a path by hand.
// Each builder returns expo-router's object form, so params stay typed when a route gains some. Optional params
// that aren't given are left out of `params`.
export interface Link<P extends string> {
  pathname: P;
  params: Record<string, string>;
}

export const BACKLOG_LEVELS = ['epics', 'features', 'stories'] as const;
export type BacklogLevelParam = (typeof BACKLOG_LEVELS)[number];

export const SETUP_STEPS = ['models', 'microphone', 'downloading'] as const;
export type SetupStep = (typeof SETUP_STEPS)[number];

export interface BacklogLinkOptions {
  level?: BacklogLevelParam;
  // Keys, such as SHIP-12 (E11).
  epic?: string;
  feature?: string;
}

export const isBacklogLevel = (value: unknown): value is BacklogLevelParam =>
  (BACKLOG_LEVELS as readonly unknown[]).includes(value);

export const isSetupStep = (value: unknown): value is SetupStep =>
  (SETUP_STEPS as readonly unknown[]).includes(value);

function params(values: Record<string, string | undefined>): Record<string, string> {
  const given: Record<string, string> = {};
  for (const [name, value] of Object.entries(values)) if (value !== undefined) given[name] = value;
  return given;
}

export const links = {
  signIn: (): Link<'/sign-in'> => ({ pathname: '/sign-in', params: {} }),
  projects: (): Link<'/'> => ({ pathname: '/', params: {} }),
  account: (): Link<'/account'> => ({ pathname: '/account', params: {} }),

  // The project's tabs.
  inbox: (projectKey: string, from?: 'setup'): Link<'/p/[key]/inbox'> => ({
    pathname: '/p/[key]/inbox',
    params: params({ key: projectKey, from }),
  }),
  backlog: (projectKey: string, opts: BacklogLinkOptions = {}): Link<'/p/[key]/backlog'> => ({
    pathname: '/p/[key]/backlog',
    params: params({ key: projectKey, level: opts.level, epic: opts.epic, feature: opts.feature }),
  }),
  features: (projectKey: string): Link<'/p/[key]/features'> => ({
    pathname: '/p/[key]/features',
    params: { key: projectKey },
  }),
  retro: (projectKey: string): Link<'/p/[key]/retro'> => ({
    pathname: '/p/[key]/retro',
    params: { key: projectKey },
  }),

  // The project's stack screens, which cover the tab bar.
  item: (projectKey: string, key: string): Link<'/p/[key]/items/[itemKey]'> => ({
    pathname: '/p/[key]/items/[itemKey]',
    params: { key: projectKey, itemKey: key },
  }),
  featureBoard: (
    projectKey: string,
    featureKey: string,
  ): Link<'/p/[key]/features/[featureKey]/board'> => ({
    pathname: '/p/[key]/features/[featureKey]/board',
    params: { key: projectKey, featureKey },
  }),
  digest: (projectKey: string): Link<'/p/[key]/features/digest'> => ({
    pathname: '/p/[key]/features/digest',
    params: { key: projectKey },
  }),

  // This phone's settings and the voice setup.
  settingsVoice: (): Link<'/settings/voice'> => ({ pathname: '/settings/voice', params: {} }),
  licences: (): Link<'/settings/voice/licences'> => ({
    pathname: '/settings/voice/licences',
    params: {},
  }),
  setup: (step: SetupStep): Link<'/setup/[step]'> => ({
    pathname: '/setup/[step]',
    params: { step },
  }),
};

// The web page of a work item, or of a Feature's board, on the signed-in server. The SPA's patterns are
// /p/:key/items/:itemKey and /p/:key/items/:itemKey/board. Callers pass platform().apiOrigin().
export function webUrl(
  origin: string,
  projectKey: string,
  itemKey: string,
  page: 'item' | 'board' = 'item',
): string {
  const item = `${origin}/p/${projectKey}/items/${itemKey}`;
  return page === 'board' ? `${item}/board` : item;
}

// A project page on the web, for an action the phone can't take (voice D7): the web Backlog at a level, or its
// Planning view. A backlog page with no level is the level the phone screen shows; webPageUrl takes Stories then.
export type WebPage =
  | { page: 'backlog'; level?: BacklogLevelParam }
  | { page: 'planning' }
  // The retro Insights page, and a sprint's retro board (the retro search when the sprint isn't known).
  | { page: 'retroInsights' }
  | { page: 'sprintRetro'; sprintId?: string };

// The web page of a project page on the signed-in server. The SPA's patterns are /p/:key/backlogs/:level, and the
// Planning view is /p/:key/backlogs/stories?mode=planning (it exists on the Stories level only). Callers pass
// platform().apiOrigin().
export function webPageUrl(origin: string, projectKey: string, page: WebPage): string {
  const project = `${origin}/p/${projectKey}`;
  switch (page.page) {
    case 'planning':
      return `${project}/backlogs/stories?mode=planning`;
    case 'retroInsights':
      return `${project}/retros/insights`;
    case 'sprintRetro':
      return page.sprintId === undefined
        ? `${project}/retros`
        : `${project}/sprints/${page.sprintId}/retro`;
    case 'backlog':
      return `${project}/backlogs/${page.level ?? 'stories'}`;
  }
}
