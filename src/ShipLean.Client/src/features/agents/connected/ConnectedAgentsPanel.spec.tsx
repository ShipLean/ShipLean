import { expect, test } from '@playwright/experimental-ct-react';
import type { ConnectedAgentDto } from '../../../api/agents';
import { agentsTestIds as ids } from '../testIds';
import type { StubbablePage } from '../authorize/stubPage';
import { ConnectedAgentsPanel } from './ConnectedAgentsPanel';

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

async function stubList(page: StubbablePage, list: ConnectedAgentDto[]) {
  await page.route('**/api/me/agents', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(list) }),
  );
  await page.route('**/api/agents/mcp-url', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ mcpUrl: 'https://ship.example.test/mcp' }),
    }),
  );
}

test('ConnectedAgentsPanel_WhenAgentsConnected_ShouldListEachWithItsDetails', async ({
  mount,
  page,
}) => {
  await stubList(page, agents);
  await mount(<ConnectedAgentsPanel />);

  const rows = page.getByTestId(ids.connectedRow);
  await expect(rows).toHaveCount(2);
  await expect(rows.nth(0).getByTestId(ids.connectedName)).toHaveText('Claude Code');
  await expect(rows.nth(0).getByTestId(ids.connectedHost)).toHaveText('claude.ai');
  await expect(rows.nth(0).getByTestId(ids.connectedScopes)).toHaveText('Read · Write');
  await expect(rows.nth(0).getByTestId(ids.connectedLastUsed)).toHaveText('Last used 2h ago');
  await expect(rows.nth(0).getByTestId(ids.connectedConnectedAt)).toHaveText('Connected 20 Oct');
  await expect(rows.nth(1).getByTestId(ids.connectedHost)).toHaveText('unverified');
  await expect(rows.nth(1).getByTestId(ids.connectedLastUsed)).toHaveText('Never used');
});

test('ConnectedAgentsPanel_WhenRevokeClicked_ShouldDeleteThatAgentAndReload', async ({
  mount,
  page,
}) => {
  let current = agents;
  await page.route('**/api/me/agents', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(current) }),
  );
  await page.route('**/api/agents/mcp-url', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: '{"mcpUrl":"https://x/mcp"}',
    }),
  );
  const deleted: string[] = [];
  await page.route('**/api/me/agents/*', (route) => {
    deleted.push(new URL(route.request().url()).pathname);
    current = current.filter((a) => !route.request().url().endsWith(a.id));
    return route.fulfill({ status: 204 });
  });
  await mount(<ConnectedAgentsPanel />);

  await page.getByTestId(ids.connectedRow).nth(1).getByTestId(ids.connectedRevoke).click();

  await expect(page.getByTestId(ids.connectedRow)).toHaveCount(1);
  expect(deleted).toEqual(['/api/me/agents/a-2']);
});

test('ConnectedAgentsPanel_WhenNone_ShouldSaySoAndStillShowTheAddLine', async ({ mount, page }) => {
  await stubList(page, []);
  await mount(<ConnectedAgentsPanel />);

  await expect(page.getByTestId(ids.connectedEmpty)).toBeVisible();
  await expect(page.getByTestId(ids.connectedAddLine)).toHaveText(
    'claude mcp add --transport http shiplean https://ship.example.test/mcp',
  );
});
