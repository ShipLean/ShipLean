import { expect, test } from '@playwright/experimental-ct-react';
import type {
  AgentActivityDto,
  AgentPanelDto,
  AgentSessionDto,
  AgentStartAvailabilityDto,
} from '../../api/agentSessions';
import { mei, story } from '../workItem/ct/fixtures';
import { stubApi } from '../workItem/ct/stubApi';
import { stubComments } from '../workItem/ct/stubDetails';
import { WorkItemDetailsHarness } from '../workItem/ct/WorkItemDetailsHarness';
import { workItemTestIds } from '../workItem/testIds';
import { agentPanelTestIds as ids } from './testIds';

const route = { hooksConfig: { route: '/p/SHIP/items/SHIP-141' } };

const activity = (
  id: string,
  type: AgentActivityDto['type'],
  body: string | null,
  extra: Partial<AgentActivityDto> = {},
): AgentActivityDto => ({
  id,
  type,
  body,
  action: null,
  parameter: null,
  result: null,
  ephemeral: false,
  signal: null,
  createdAt: '2026-10-28T09:00:00+00:00',
  createdLabel: '5 minutes ago',
  ...extra,
});

const session = (id: string, extra: Partial<AgentSessionDto> = {}): AgentSessionDto => ({
  id,
  connectionName: 'Relay',
  state: 'working',
  live: true,
  stopRequested: false,
  startedAt: '2026-10-28T08:50:00+00:00',
  startedLabel: '15 minutes ago',
  lastActivityAt: '2026-10-28T09:00:00+00:00',
  lastActivityLabel: '5 minutes ago',
  endedLabel: null,
  urls: [],
  plan: [],
  activities: [],
  ...extra,
});

const cannotStart: AgentStartAvailabilityDto = {
  canStart: false,
  reason: 'liveSession',
  message: 'An agent session is already running on this item.',
};
const canStart: AgentStartAvailabilityDto = { canStart: true, reason: null, message: '' };

// Stubs the panel's read, Start and Stop. A Stop marks the current session as stopping; a Start opens a new live session.
async function stubPanel(
  page: Parameters<typeof stubComments>[0],
  initial: AgentPanelDto,
  options: { failStop?: string } = {},
) {
  let panel = initial;
  const calls: string[] = [];
  await page.route('**/api/work-items/*/agent-sessions', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(panel),
    }),
  );
  await page.route('**/api/work-items/*/agent-session/stop', (route) => {
    calls.push('stop');
    if (options.failStop) {
      return route.fulfill({
        status: 409,
        contentType: 'application/json',
        body: JSON.stringify({ error: options.failStop, code: 'conflict' }),
      });
    }
    const [current, ...rest] = panel.sessions;
    panel = { ...panel, sessions: [{ ...current, stopRequested: true }, ...rest] };
    return route.fulfill({ status: 204, contentType: 'application/json', body: '' });
  });
  await page.route('**/api/work-items/*/agent-session', (route) => {
    calls.push('start');
    panel = {
      ...panel,
      availability: cannotStart,
      sessions: [session('s-new', { state: 'submitted' }), ...panel.sessions],
    };
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ sessionId: 's-new', state: 'submitted' }),
    });
  });
  return { calls };
}

async function mountWith(
  mount: (
    node: React.JSX.Element,
    options: typeof route,
  ) => Promise<import('@playwright/experimental-ct-react').MountResult>,
  page: Parameters<typeof stubComments>[0],
) {
  await stubApi(page, story);
  await stubComments(page, mei, []);
  return mount(<WorkItemDetailsHarness itemKey="SHIP-141" />, route);
}

test('AgentPanel_WhenNoSessionAndNoConnection_ShouldStayOut', async ({ mount, page }) => {
  await stubPanel(page, {
    visible: false,
    availability: { canStart: false, reason: 'noConnection', message: 'No connection.' },
    sessions: [],
  });
  const screen = await mountWith(mount, page);

  await expect(screen.getByTestId(workItemTestIds.commentsEmpty)).toBeVisible();
  await expect(screen.getByTestId(ids.panel)).toHaveCount(0);
});

test('AgentPanel_WhenASessionIsLive_ShouldShowHeaderLinksPlanAndActivities', async ({
  mount,
  page,
}) => {
  await stubPanel(page, {
    visible: true,
    availability: cannotStart,
    sessions: [
      session('s1', {
        urls: [{ label: 'Pull request', url: 'https://github.com/ShipLean/ShipLean/pull/9' }],
        plan: [
          { content: 'Write the test', status: 'completed' },
          { content: 'Fix it', status: 'inProgress' },
          { content: 'Ship it', status: 'pending' },
          { content: 'Old idea', status: 'canceled' },
        ],
        activities: [
          activity('a1', 'thought', 'Looking at the form'),
          activity('a2', 'action', null, { action: 'Edited', parameter: 'Form.tsx', result: 'ok' }),
          activity('a3', 'action', 'Running tests', { ephemeral: true }),
        ],
      }),
    ],
  });
  const screen = await mountWith(mount, page);

  await expect(screen.getByTestId(ids.connection)).toHaveText('Relay');
  await expect(screen.getByTestId(ids.state)).toContainText('Working');
  await expect(screen.getByTestId(ids.started)).toHaveText('Started 15 minutes ago');
  await expect(screen.getByTestId(ids.lastActivity)).toHaveText('Last activity 5 minutes ago');
  await expect(screen.getByTestId(ids.link)).toHaveAttribute(
    'href',
    'https://github.com/ShipLean/ShipLean/pull/9',
  );
  const steps = screen.getByTestId(ids.planStep);
  await expect(steps).toHaveCount(4);
  await expect(steps.nth(0)).toHaveAttribute('data-status', 'completed');
  await expect(steps.nth(1)).toHaveAttribute('data-status', 'inProgress');
  await expect(steps.nth(3)).toHaveAttribute('data-status', 'canceled');
  await expect(screen.getByTestId(ids.activity)).toHaveCount(2);
  await expect(screen.getByTestId(ids.activity).nth(1)).toContainText('Edited Form.tsx');
  await expect(screen.getByTestId(ids.activityEphemeral)).toContainText('Running tests');
  await expect(screen.getByTestId(ids.stop)).toBeVisible();
  await expect(screen.getByTestId(ids.start)).toHaveCount(0);
  await expect(screen.getByTestId(ids.startBlocked)).toHaveCount(0);
});

test('AgentPanel_WhenTheAgentAsks_ShouldHighlightTheQuestionAndSayToReplyInAComment', async ({
  mount,
  page,
}) => {
  await stubPanel(page, {
    visible: true,
    availability: cannotStart,
    sessions: [
      session('s1', {
        state: 'inputRequired',
        activities: [activity('a1', 'elicitation', 'Which branch should I use?')],
      }),
    ],
  });
  const screen = await mountWith(mount, page);

  await expect(screen.getByTestId(ids.state)).toContainText('Needs input');
  await expect(screen.getByTestId(ids.elicitation)).toContainText('Which branch should I use?');
  await expect(screen.getByTestId(ids.elicitationHint)).toContainText('Reply in a comment');
});

test('AgentPanel_WhenStopIsPressed_ShouldCallStopAndShowStopping', async ({ mount, page }) => {
  const stub = await stubPanel(page, {
    visible: true,
    availability: cannotStart,
    sessions: [session('s1')],
  });
  const screen = await mountWith(mount, page);

  await screen.getByTestId(ids.stop).click();

  await expect(screen.getByTestId(ids.stopping)).toBeVisible();
  await expect(screen.getByTestId(ids.stop)).toHaveCount(0);
  expect(stub.calls).toEqual(['stop']);
});

test('AgentPanel_WhenStopIsRefused_ShouldShowTheError', async ({ mount, page }) => {
  await stubPanel(
    page,
    { visible: true, availability: cannotStart, sessions: [session('s1')] },
    { failStop: 'No agent session is running on this item.' },
  );
  const screen = await mountWith(mount, page);

  await screen.getByTestId(ids.stop).click();

  await expect(screen.getByTestId(ids.actionError)).toHaveText(
    'No agent session is running on this item.',
  );
});

test('AgentPanel_WhenNoSessionIsLiveAndStartWorks_ShouldOfferStartAgent', async ({
  mount,
  page,
}) => {
  const stub = await stubPanel(page, {
    visible: true,
    availability: canStart,
    sessions: [session('s0', { state: 'completed', live: false, endedLabel: '1 hour ago' })],
  });
  const screen = await mountWith(mount, page);

  await expect(screen.getByTestId(ids.stop)).toHaveCount(0);
  await screen.getByTestId(ids.start).click();

  await expect(screen.getByTestId(ids.stop)).toBeVisible();
  await expect(screen.getByTestId(ids.start)).toHaveCount(0);
  expect(stub.calls).toEqual(['start']);
});

test('AgentPanel_WhenStartIsBlocked_ShouldSayWhatIsMissing', async ({ mount, page }) => {
  await stubPanel(page, {
    visible: true,
    availability: {
      canStart: false,
      reason: 'noRepository',
      message: "This project has no repository. Add its URL in the project's General settings.",
    },
    sessions: [],
  });
  const screen = await mountWith(mount, page);

  await expect(screen.getByTestId(ids.startBlocked)).toContainText('no repository');
  await expect(screen.getByTestId(ids.start)).toHaveCount(0);
  await expect(screen.getByTestId(ids.stop)).toHaveCount(0);
});

test('AgentPanel_WhenEarlierSessionsExist_ShouldFoldThemUntilExpanded', async ({ mount, page }) => {
  await stubPanel(page, {
    visible: true,
    availability: canStart,
    sessions: [
      session('s2', {
        state: 'failed',
        live: false,
        endedLabel: '1 hour ago',
        activities: [activity('b1', 'thought', 'Newest session thought')],
      }),
      session('s1', {
        state: 'completed',
        live: false,
        endedLabel: '2 days ago',
        activities: [activity('a1', 'thought', 'Old session thought')],
      }),
    ],
  });
  const screen = await mountWith(mount, page);

  await expect(screen.getByTestId(ids.state)).toHaveCount(2);
  await expect(screen.getByText('Newest session thought')).toBeVisible();
  await expect(screen.getByText('Old session thought')).toBeHidden();
  await screen.getByTestId(ids.earlierSession).locator('summary').click();
  await expect(screen.getByText('Old session thought')).toBeVisible();
  await expect(screen.getByTestId(ids.lastActivity).nth(1)).toHaveText('Ended 2 days ago');
});
