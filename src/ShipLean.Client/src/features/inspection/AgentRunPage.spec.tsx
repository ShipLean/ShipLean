import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { stubInspection } from './ct/inspectionMocks';
import { AgentRunPageHarness } from './page/ct/AgentRunPageHarness';
import { inspectionTestIds as ids } from './testIds';

const route = '/p/SHIP/items/SHIP-1/runs/sess-cc-1';

async function setup(page: Page, width: number) {
  await page.setViewportSize({ width, height: 900 });
  const seen = await stubInspection(page);
  await page.route('**/api/projects/by-key/*', (r) =>
    r.fulfill({ json: { id: 'p1', key: 'SHIP', role: 'member' } }),
  );
  return seen;
}

test('AgentRunPage_WhenUnder900px_ShouldShowOneLineAndALinkBackAndFetchNoRun', async ({
  mount,
  page,
}) => {
  const seen = await setup(page, 899);
  await mount(<AgentRunPageHarness />, { hooksConfig: { route } });
  await expect(page.getByTestId(ids.runPageNarrow)).toContainText(
    'Agent runs open on a wider screen',
  );
  await expect(page.getByTestId(ids.runPageNarrowBack)).toHaveAttribute(
    'href',
    '/p/SHIP/items/SHIP-1',
  );
  await expect(page.getByTestId(ids.runPage)).toHaveCount(0);
  expect(seen.urls.filter((u) => /\/runs\//.test(u))).toEqual([]);
});

test('AgentRunPage_WhenAt900px_ShouldShowTheRunPageNotTheNarrowLine', async ({ mount, page }) => {
  await setup(page, 900);
  await mount(<AgentRunPageHarness />, { hooksConfig: { route } });
  await expect(page.getByTestId(ids.runAgent)).toHaveText('Claude Code');
  await expect(page.getByTestId(ids.runPageNarrow)).toHaveCount(0);
});
