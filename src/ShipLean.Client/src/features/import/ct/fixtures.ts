import type { Page, Route } from 'playwright';
import type {
  ImportErrorDto,
  ImportJobDto,
  ImportLogLineDto,
  ImportMappingDto,
  JiraConnectionDto,
  JiraProjectDto,
  JiraStatusCategory,
  StatusMappingDto,
} from '../../../api/import';
import type { MemberDto, ProjectDto, StateDto } from '../../../api/projects';
import type { UserRef } from '../../../api/refs';

export const project: ProjectDto = {
  id: 'p-ship',
  key: 'SHIP',
  name: 'Storefront',
  description: '',
  role: 'admin',
  memberCount: 3,
  workingDays: [1, 2, 3, 4, 5],
  velocityWindow: 3,
  velocityOverride: null,
  nextItemNumber: 1,
};

export const connection: JiraConnectionDto = {
  id: 'c1',
  projectId: project.id,
  baseUrl: 'https://acme.atlassian.net',
  kind: 'cloud',
  email: 'priya@acme.io',
  tokenLastFour: '3fA9',
  createdBy: null,
  createdAt: '2026-10-20T10:00:00Z',
  createdLabel: '4 days ago',
};

export const jiraProjects: JiraProjectDto[] = [
  { key: 'PLATFORM', name: 'Platform Team', issueCount: 1342, importedAs: null },
  {
    key: 'MOBILE',
    name: 'Mobile App',
    issueCount: 1284,
    importedAs: {
      projectKey: 'MOB',
      jobId: 'j1',
      finishedAt: '2026-10-22T10:00:00Z',
      finishedLabel: '2 days ago',
    },
  },
];

const base = `**/api/projects/${project.id}`;

export const json = (route: Route, body: unknown, status = 200) =>
  route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) });

// Stubs the connection and Jira project reads of the import; a null connection answers 204.
export async function stubImport(
  page: Page,
  saved: JiraConnectionDto | null,
  projects = jiraProjects,
) {
  await page.route(`${base}/jira/connection`, (route) =>
    route.request().method() === 'GET'
      ? saved === null
        ? route.fulfill({ status: 204 })
        : json(route, saved)
      : route.fallback(),
  );
  await page.route(`${base}/jira/projects`, (route) => json(route, projects));
}

const user = (id: string, name: string, initials: string): UserRef => ({
  id,
  name,
  initials,
  avatarColour: '#3f7d5a',
  photoUrl: null,
});
export const priya = user('u-priya', 'Priya Shah', 'PS');
export const sam = user('u-sam', 'Sam Okoye', 'SO');

export const states: StateDto[] = [
  {
    id: 's1',
    type: 'story',
    name: 'New',
    category: 'proposed',
    order: 1,
    itemCount: 0,
    startsAgent: false,
  },
  {
    id: 's2',
    type: 'story',
    name: 'Active',
    category: 'inProgress',
    order: 2,
    itemCount: 0,
    startsAgent: false,
  },
  {
    id: 's3',
    type: 'story',
    name: 'Closed',
    category: 'completed',
    order: 3,
    itemCount: 0,
    startsAgent: false,
  },
  {
    id: 's4',
    type: 'bug',
    name: 'New',
    category: 'proposed',
    order: 1,
    itemCount: 0,
    startsAgent: false,
  },
  {
    id: 's5',
    type: 'bug',
    name: 'Closed',
    category: 'completed',
    order: 2,
    itemCount: 0,
    startsAgent: false,
  },
  {
    id: 's6',
    type: 'task',
    name: 'To Do',
    category: 'proposed',
    order: 1,
    itemCount: 0,
    startsAgent: false,
  },
];

export const members: MemberDto[] = [priya, sam].map((u) => ({
  user: u,
  email: `${u.id}@acme.io`,
  role: 'member',
  status: 'active',
}));

export const mappingDto: ImportMappingDto = {
  issueTypes: [
    { jiraType: 'Story', count: 900, target: 'story', note: null, isNewSinceLastRun: false },
    { jiraType: 'Bug', count: 300, target: 'bug', note: null, isNewSinceLastRun: false },
    { jiraType: 'Spike', count: 12, target: 'skip', note: 'Skipped', isNewSinceLastRun: false },
  ],
  statuses: [
    status('Story', 'Backlog', 'new', 'New', false),
    status('Story', 'In Review', 'indeterminate', 'In Review', true),
    status('Bug', 'Backlog', 'new', 'New', false),
    status('Spike', 'Open', 'new', 'Open', false),
  ],
  users: [
    {
      jiraAccountId: 'a1',
      name: 'Priya S',
      email: 'priya@acme.io',
      references: 120,
      resolution: 'matched',
      user: priya,
      isBot: false,
      isNewSinceLastRun: false,
    },
    {
      jiraAccountId: 'a2',
      name: 'Dev Patel',
      email: 'dev@acme.io',
      references: 40,
      resolution: 'create',
      user: null,
      isBot: false,
      isNewSinceLastRun: false,
    },
    {
      jiraAccountId: 'a3',
      name: 'Jira Automation',
      email: null,
      references: 7,
      resolution: 'jiraImportUser',
      user: null,
      isBot: true,
      isNewSinceLastRun: false,
    },
  ],
  fields: {
    storyPointsFieldId: 'customfield_10016',
    candidates: [
      { fieldId: 'customfield_10016', name: 'Story point estimate', populated: 800, missing: 100 },
      { fieldId: 'customfield_10020', name: 'Size', populated: 20, missing: 880 },
    ],
    includeComponents: true,
    includeComments: true,
    includeAttachments: false,
    includeChangelog: true,
  },
  totals: {
    issues: 1200,
    comments: 3912,
    attachments: 0,
    revisions: 21406,
    sprints: 11,
    people: 3,
  },
  warnings: [
    { severity: 'warning', section: 'types', message: '12 Spike issues will not be imported.' },
    { severity: 'info', section: 'users', message: '1 person has no email in Jira.' },
  ],
};

function status(
  jiraType: string,
  jiraStatus: string,
  jiraCategory: JiraStatusCategory,
  targetState: string,
  createsState: boolean,
): StatusMappingDto {
  return {
    jiraType,
    jiraStatus,
    jiraCategory,
    count: 10,
    targetState,
    targetCategory: jiraCategory === 'new' ? 'proposed' : 'inProgress',
    createsState,
    isNewSinceLastRun: false,
  };
}

/** A re-run's mapping: Story · In Review is a status Jira gained since the last run. */
export const mappingWithNewStatus: ImportMappingDto = {
  ...mappingDto,
  statuses: mappingDto.statuses.map((s) =>
    s.jiraStatus === 'In Review' ? { ...s, isNewSinceLastRun: true } : s,
  ),
};

// Stubs the project's states and members and the mapping read; returns the request bodies the mapping read received.
export async function stubMapping(page: Page, mapping: ImportMappingDto = mappingDto) {
  const requests: unknown[] = [];
  await page.route(`${base}/states`, (route) => json(route, states));
  await page.route(`${base}/members`, (route) => json(route, members));
  await page.route('**/api/import/mapping', (route) => {
    requests.push(route.request().postDataJSON());
    return json(route, mapping);
  });
  return requests;
}

const entity = (done: number, total: number, failed = 0) => ({ done, total, failed });

/** A job of the fixture project; override what the case needs. */
export function importJob(overrides: Partial<ImportJobDto> = {}): ImportJobDto {
  return {
    id: 'job1',
    jiraProjectKey: 'PLATFORM',
    projectKey: 'SHIP',
    projectId: project.id,
    status: 'running',
    progress: {
      percent: 40,
      step: 'issues',
      message: 'Issues',
      rerun: false,
      counts: {
        sprints: entity(11, 11),
        issues: entity(480, 1200),
        comments: entity(0, 3912),
        attachments: entity(0, 0),
        revisions: entity(0, 21406),
      },
      usersMatched: 0,
      usersCreated: 0,
      statesCreated: 0,
      rerunResult: null,
    },
    startedAt: '2026-10-24T10:00:00Z',
    startedLabel: 'just now',
    finishedAt: null,
    finishedLabel: null,
    createdBy: null,
    ...overrides,
  };
}

export const logLines: ImportLogLineDto[] = [
  {
    id: 1,
    loggedAt: '2026-10-24T10:00:01Z',
    timeLabel: '10:00:01',
    level: 'info',
    message: 'Using empty project SHIP',
  },
  {
    id: 2,
    loggedAt: '2026-10-24T10:00:09Z',
    timeLabel: '10:00:09',
    level: 'warning',
    message: 'Sprint 3 ends on a Sunday',
  },
];

export const failedRow: ImportErrorDto = {
  id: 'e1',
  step: 'attachments',
  entity: 'attachment',
  sourceKey: 'PLATFORM-406/10601',
  message: 'design-review.mov is 41 MB (limit 25 MB)',
  resolution: 'pending',
};

// Stubs one job's reads under /api/import/jobs/{id}; `job` may be a function that answers each call in turn.
export async function stubJob(
  page: Page,
  job: ImportJobDto | (() => ImportJobDto),
  log: ImportLogLineDto[] = logLines,
  errors: ImportErrorDto[] | (() => ImportErrorDto[]) = [],
) {
  const id = typeof job === 'function' ? job().id : job.id;
  await page.route(`**/api/import/jobs/${id}`, (route) =>
    route.request().method() === 'GET'
      ? json(route, typeof job === 'function' ? job() : job)
      : route.fallback(),
  );
  await page.route(`**/api/import/jobs/${id}/log`, (route) => json(route, log));
  await page.route(`**/api/import/jobs/${id}/errors`, (route) =>
    json(route, typeof errors === 'function' ? errors() : errors),
  );
}
