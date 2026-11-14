import { expect, test } from '@playwright/experimental-ct-react';
import type { Locator } from 'playwright';
import type { ConnectedAgentDto } from '@shiplean/core/api/agents';
import type { ProjectSummaryDto } from '@shiplean/core/api/projects';
import { agentsTestIds as agentIds } from '../../../agents/testIds';
import { settingsTestIds as ids } from '../../testIds';
import { AccountPanelsHarness } from './ct/AccountPanelsHarness';

// Your projects, Sessions and Connected agents on a phone (390×844, SHIP-733 L733b), as element shots of these panels
// only. Screenshots are compared only in CI; the assertions around them are what fail locally.
// Regenerate with scripts/update-page-shots.sh src/features/settings/profile/account/Account.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;

const projects: ProjectSummaryDto[] = [
  { id: 'id-SHIP', key: 'SHIP', name: 'ShipLean product work', role: 'admin', memberCount: 3 },
  { id: 'id-PAY', key: 'PAY', name: 'Payments', role: 'member', memberCount: 5 },
];

const agents: ConnectedAgentDto[] = [
  {
    id: 'a-1',
    clientName: 'Claude Code',
    clientHost: 'claude.ai',
    verified: true,
    scopes: ['shiplean:read', 'shiplean:write'],
    connectedAt: '2026-10-20T09:30:00+00:00',
    lastUsedAt: '2026-10-24T13:05:41+00:00',
    connectedLabel: '20 Oct',
    lastUsedLabel: '2h ago',
  },
  {
    id: 'a-2',
    clientName: 'Review Bot',
    clientHost: null,
    verified: false,
    scopes: ['shiplean:read'],
    connectedAt: '2026-10-21T08:00:00+00:00',
    lastUsedAt: null,
    connectedLabel: 'Yesterday',
    lastUsedLabel: null,
  },
];

const PANEL_IDS = [ids.myProjectsPanel, ids.sessionsPanel, agentIds.slotConnectedAgents];

const MCP_URL = 'https://averylonghostnamewithnobreakpointsatall.internal.example.test/mcp';
const LINE = `claude mcp add --transport http shiplean ${MCP_URL}`;

interface Routable {
  route(
    url: string,
    handler: (route: {
      request(): { method(): string; url(): string };
      fulfill(response: { json?: unknown; status?: number }): Promise<void>;
    }) => Promise<void>,
  ): Promise<unknown>;
}

const stub = async (page: Routable, list: ConnectedAgentDto[] = agents) => {
  await page.route('**/api/**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (request.method() !== 'GET') return route.fulfill({ status: 204 });
    if (path === '/api/me/agents') return route.fulfill({ json: list });
    if (path === '/api/agents/mcp-url') return route.fulfill({ json: { mcpUrl: MCP_URL } });
    if (path === '/api/projects') return route.fulfill({ json: projects });
    return route.fulfill({ status: 404 });
  });
};

const box = async (locator: Locator) => {
  const b = await locator.boundingBox();
  if (!b) throw new Error('the element should render');
  return b;
};

for (const theme of THEMES) {
  test(`AccountPhone_WhenLeaveAsked_ShouldMatchItsScreenshot_profile-leave_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page);
    const view = await mount(<AccountPanelsHarness />);
    const row = view.locator('[data-key="PAY"]');
    await expect(row).toBeVisible();

    const rowBox = await box(row);
    const leaveBox = await box(row.getByTestId(ids.myProjectLeave));
    expect(leaveBox.height).toBeGreaterThanOrEqual(32);
    expect(leaveBox.x + leaveBox.width).toBeGreaterThan(rowBox.x + rowBox.width - 20);
    const key = await box(row.getByTestId(ids.myProjectKey));
    const name = await box(row.getByText('Payments'));
    const role = await box(row.getByTestId(ids.myProjectRole));
    expect(name.y).toBeLessThan(role.y);
    expect(name.x).toBeGreaterThan(key.x + key.width - 1);
    expect(role.x).toBe(name.x);

    await row.getByTestId(ids.myProjectLeave).tap();
    const cancel = await box(view.getByTestId(ids.myProjectLeaveCancel));
    const leave = await box(view.getByTestId(ids.myProjectLeaveConfirm));
    expect(Math.abs(cancel.width - leave.width)).toBeLessThanOrEqual(1);
    expect(cancel.y).toBeGreaterThan((await box(view.getByTestId(ids.myProjectLeaveRow))).y + 20);
    expect(cancel.y).toBe(leave.y);

    await expect(view.getByTestId(ids.myProjectsPanel)).toHaveScreenshot(
      `profile-leave.${theme}.png`,
      { animations: 'disabled' },
    );
  });

  test(`AccountPhone_WhenRevokeAsked_ShouldMatchItsScreenshot_profile-revoke_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page);
    const view = await mount(<AccountPanelsHarness />);
    const rows = view.getByTestId(agentIds.connectedRow);
    await expect(rows).toHaveCount(2);
    expect(
      (await box(rows.nth(0).getByTestId(agentIds.connectedRevoke))).height,
    ).toBeGreaterThanOrEqual(32);

    await rows.nth(1).getByTestId(agentIds.connectedRevoke).tap();
    const cancel = await box(view.getByTestId(agentIds.connectedRevokeCancel));
    const revoke = await box(view.getByTestId(agentIds.connectedRevokeConfirm));
    expect(Math.abs(cancel.width - revoke.width)).toBeLessThanOrEqual(1);
    expect(cancel.y).toBe(revoke.y);
    await expect(view.getByTestId(agentIds.connectedCopy)).toBeVisible();
    expect((await box(view.getByTestId(agentIds.connectedAddLine))).height).toBeGreaterThan(30);

    await expect(view.getByTestId(agentIds.slotConnectedAgents)).toHaveScreenshot(
      `profile-revoke.${theme}.png`,
      { animations: 'disabled' },
    );
  });

  test(`AccountPhone_WhenSessionsShown_ShouldMatchItsScreenshot_profile-sessions_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page);
    const view = await mount(<AccountPanelsHarness />);
    for (const id of [ids.signOutOthers, ids.resetViews, ids.signOutBrowser]) {
      expect((await box(view.getByTestId(id))).height).toBeGreaterThanOrEqual(32);
    }

    await expect(view.getByTestId(ids.sessionsPanel)).toHaveScreenshot(
      `profile-sessions.${theme}.png`,
      { animations: 'disabled' },
    );
  });

  test(`AccountPhone_WhenNoAgents_ShouldMatchItsScreenshot_profile-no-agents_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page, []);
    const view = await mount(<AccountPanelsHarness />);
    await expect(view.getByTestId(agentIds.connectedEmpty)).toBeVisible();
    await expect(view.getByTestId(agentIds.connectedAddLine)).toHaveText(LINE);

    await expect(view.getByTestId(agentIds.slotConnectedAgents)).toHaveScreenshot(
      `profile-no-agents.${theme}.png`,
      { animations: 'disabled' },
    );
  });
}

test('AccountPhone_WhenCopyTapped_ShouldPutTheLineOnTheClipboardAndToast', async ({
  mount,
  page,
  context,
}) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await stub(page, []);
  const view = await mount(<AccountPanelsHarness />);

  await view.getByTestId(agentIds.connectedCopy).tap();

  await expect(page.getByText('Copied the Claude Code line')).toBeVisible();
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(LINE);
});

test('AccountPhone_WhenOpened_ShouldHaveNoSidewaysScroll', async ({ mount, page }) => {
  await stub(page);
  await mount(<AccountPanelsHarness />);
  await expect(page.getByTestId(agentIds.connectedAddLine)).toBeVisible();

  const [scroll, client] = await page.evaluate(() => [
    document.documentElement.scrollWidth,
    document.documentElement.clientWidth,
  ]);
  expect(scroll).toBeLessThanOrEqual(client);
  // The Claude Code line must wrap rather than scroll inside itself, so no element in the panels overflows its box.
  const overflowing = await page.evaluate((panels) => {
    const found: string[] = [];
    for (const id of panels) {
      document.querySelectorAll(`[data-testid="${id}"], [data-testid="${id}"] *`).forEach((el) => {
        if (el.scrollWidth > el.clientWidth)
          found.push(el.getAttribute('data-testid') ?? el.tagName);
      });
    }
    return found;
  }, PANEL_IDS);
  expect(overflowing).toEqual([]);
});
