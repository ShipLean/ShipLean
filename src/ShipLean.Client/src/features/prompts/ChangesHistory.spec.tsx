import { expect, test } from '@playwright/experimental-ct-react';
import historyJson from '../../../../ShipLean.TestSupport/DtoFixtures/prompts/prompt-history.json' with { type: 'json' };
import type { PromptHistoryDto } from '@shiplean/core/api/prompts';
import savedJson from '../../../../ShipLean.TestSupport/DtoFixtures/prompts/prompt-saved.json' with { type: 'json' };
import { openByAdminFlag, openByPageJson } from './ct/openPrompt';
import { adminPage } from './ct/promptsData';
import { promptsTestIds as ids } from './testIds';

const history = historyJson as PromptHistoryDto;
const HISTORY_URL = '**/api/projects/*/prompts/brief/history';
const USE_URL = '**/api/projects/*/prompts/brief/versions/1/use';
const BRIEF_URL = '**/api/projects/*/prompts/brief';

test('YourChangesTab_WhenPatch_ShouldDrawTheDiff', async ({ mount, page }) => {
  await openByAdminFlag(mount, page, 'brief');
  await page.getByTestId(ids.tabYourChanges).click();

  await expect(page.getByTestId(ids.yourChangesDiff)).toBeVisible();
  await expect(page.getByTestId(ids.diffLine).and(page.locator('[data-kind=add]'))).toHaveText(
    /line six, reworded/,
  );
  await expect(page.getByTestId(ids.diffLine).and(page.locator('[data-kind=del]'))).toHaveCount(1);
});

test('YourChangesTab_WhenNoPatch_ShouldSayNoChanges', async ({ mount, page }) => {
  const noPatch = {
    ...adminPage,
    stages: adminPage.stages.map((s) => ({
      ...s,
      prompts: s.prompts.map((p) => (p.key === 'brief' ? { ...p, yourChangesPatch: null } : p)),
    })),
  };
  await openByAdminFlag(mount, page, 'brief', true, noPatch);
  await page.getByTestId(ids.tabYourChanges).click();

  await expect(page.getByTestId(ids.yourChangesEmpty)).toHaveText('No changes');
});

test('HistoryTab_ShouldListEveryVersionNewestFirstAndMarkTheOneInUse', async ({ mount, page }) => {
  await page.route(HISTORY_URL, (route) => route.fulfill({ json: history }));
  await openByAdminFlag(mount, page, 'brief');
  await page.getByTestId(ids.tabHistory).click();

  await expect(page.getByTestId(ids.historyRowInUse)).toContainText('v2');
  await expect(page.getByTestId(ids.historyRowInUse)).toContainText('In use');
  await expect(page.getByTestId(ids.historyRow)).toHaveCount(1);
  await expect(page.getByTestId(ids.historyRow)).toContainText('v1');
  await expect(page.getByTestId(ids.historyBuiltInRow)).toContainText('built-in of 10 Nov');
  await expect(page.getByTestId(ids.historyBuiltInUse)).toBeVisible();
});

test('HistoryTab_WhenUseClicked_ShouldPutThatVersionBack', async ({ mount, page }) => {
  await page.route(HISTORY_URL, (route) => route.fulfill({ json: history }));
  const used = page.waitForRequest(
    (r) => r.method() === 'POST' && r.url().endsWith('/versions/1/use'),
  );
  await page.route(USE_URL, (route) =>
    route.fulfill({ json: { key: 'brief', status: 'custom', inUseVersion: 1 } }),
  );
  await openByAdminFlag(mount, page, 'brief');
  await page.getByTestId(ids.tabHistory).click();

  await page.getByTestId(ids.historyUse).click();
  await expect(page.getByTestId(ids.historyNote)).toContainText(
    'Agents get v1 from their next call',
  );
  await page.getByRole('button', { name: 'Confirm' }).click();

  await used;
});

test('HistoryTab_WhenMember_ShouldOfferNoUse', async ({ mount, page }) => {
  await page.route(HISTORY_URL, (route) => route.fulfill({ json: history }));
  await openByAdminFlag(mount, page, 'brief', false);
  await page.getByTestId(ids.tabHistory).click();

  await expect(page.getByTestId(ids.historyRow)).toHaveCount(1);
  await expect(page.getByTestId(ids.historyUse)).toHaveCount(0);
  await expect(page.getByTestId(ids.historyBuiltInUse)).toHaveCount(0);
});

test('history shows the agent and MCP for a version saved over MCP', async ({ mount, page }) => {
  await page.route(HISTORY_URL, (route) => route.fulfill({ json: history }));
  await openByAdminFlag(mount, page, 'brief');
  await page.getByTestId(ids.tabHistory).click();

  const row = page.getByTestId(ids.historyRow);
  await expect(row).toContainText('Maria Santos');
  await expect(page.getByTestId(ids.historyAgent)).toHaveText('via Claude Code (claude.ai) Agent');
  await expect(row).toContainText('MCP');
});

test('history shows no agent line for an editor version', async ({ mount, page }) => {
  await page.route(HISTORY_URL, (route) => route.fulfill({ json: history }));
  await openByAdminFlag(mount, page, 'brief');
  await page.getByTestId(ids.tabHistory).click();

  await expect(page.getByTestId(ids.historyRowInUse)).toContainText('Editor');
  await expect(page.getByTestId(ids.historyRowInUse).getByTestId(ids.historyAgent)).toHaveCount(0);
});

test('UseBuiltInDialog_WhenConfirmed_ShouldPostUseBuiltIn', async ({ mount, page }) => {
  let posted = 0;
  await page.route(`${BRIEF_URL}/use-built-in`, (route) => {
    posted++;
    return route.fulfill({ json: savedJson });
  });
  await openByPageJson(mount, page, 'brief');
  await page.getByTestId(ids.actionUseBuiltIn).click();
  await expect(page.getByTestId(ids.useBuiltInDialog)).toContainText('Runs already going keep');

  await page.getByTestId(ids.useBuiltInConfirm).click();

  await expect.poll(() => posted).toBe(1);
  await expect(page.getByTestId(ids.useBuiltInDialog)).toHaveCount(0);
});

test('UseBuiltInDialog_WhenCancelled_ShouldPostNothing', async ({ mount, page }) => {
  let posted = 0;
  await page.route(`${BRIEF_URL}/use-built-in`, (route) => {
    posted++;
    return route.fulfill({ json: savedJson });
  });
  await openByPageJson(mount, page, 'brief');
  await page.getByTestId(ids.actionUseBuiltIn).click();

  await page.getByTestId(ids.useBuiltInCancel).click();

  await expect(page.getByTestId(ids.useBuiltInDialog)).toHaveCount(0);
  expect(posted).toBe(0);
});
