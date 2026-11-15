import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { RunPageDto } from '@shiplean/core/api/inspection';
import { claudeRunPage, stubInspection } from './ct/inspectionMocks';
import { inspectionTestIds as ids } from './testIds';
import { RunPageHarness } from './page/ct/RunPageHarness';

const route = '/p/SHIP/items/SHIP-1/runs/sess-cc-1';

// The server filters errors-only; the stub answers it with the two failing rows of the synthetic run.
const errorRow = (id: string, name: string, startMs: number) => ({
  ...claudeRunPage.turns[0].rows[0],
  id,
  name,
  startMs,
  endMs: startMs + 2000,
  durationMs: 2000,
  isError: true,
});
const errorsPage: RunPageDto = {
  ...claudeRunPage,
  turns: [
    {
      ...claudeRunPage.turns[0],
      rows: [errorRow('e1', 'Bash', 21000), errorRow('e2', 'Edit', 30000)],
    },
    { ...claudeRunPage.turns[1], rows: [] },
  ],
};

async function setup(page: Page, status = 200) {
  await page.setViewportSize({ width: 1440, height: 900 });
  const seen = await stubInspection(page);
  const gets: string[] = [];
  const posts: string[] = [];
  await page.route('**/api/projects/by-key/*', (r) =>
    r.fulfill({ json: { id: 'p1', key: 'SHIP', role: 'member' } }),
  );
  await page.route('**/api/inspection/projects/*/items/*/runs/*', (r) => {
    const url = new URL(r.request().url());
    if (/\/rows\//.test(url.pathname)) return r.fallback();
    gets.push(url.search);
    if (status !== 200)
      return r.fulfill({
        status,
        json: { code: 'conflict', error: 'ClickHouse is not reachable: connection refused' },
      });
    return r.fulfill({
      json: url.searchParams.get('errorsOnly') === 'true' ? errorsPage : claudeRunPage,
    });
  });
  page.on('request', (r) => {
    if (r.method() === 'POST' && r.url().endsWith('/usage/inspection'))
      posts.push(JSON.parse(r.postData() ?? '{}').action);
  });
  return { seen, gets, posts };
}

const rows = (page: Page) => page.getByTestId(ids.runRow);

test('RunPage_WhenLoaded_ShouldShowHeaderTotalsAndPostRunOpenedOnce', async ({ mount, page }) => {
  const { gets, posts } = await setup(page);
  await mount(<RunPageHarness />, { hooksConfig: { route } });
  await expect(page.getByTestId(ids.runAgent)).toHaveText('Claude Code');
  await expect(page.getByTestId(ids.runTotalTook)).toHaveText('1 m 25 s');
  await expect(page.getByTestId(ids.runTotalTokens)).toHaveText('7,000 / 1,470');
  await expect(page.getByTestId(ids.runTotalErrors)).toHaveText('4');
  await expect(page.getByTestId(ids.runOlder)).toHaveAttribute('href', /runs\/sess-cc-0$/);
  await expect(page.getByTestId(ids.runNewer)).toHaveAttribute('aria-disabled', 'true');
  await expect(page.getByTestId(ids.runTurn)).toHaveCount(2);
  expect(gets).toHaveLength(1);
  await expect.poll(() => posts.filter((a) => a === 'runOpened').length).toBe(1);
});

test('RunPage_WhenTurnClicked_ShouldNarrowTheAxisToThatTurnAndPostZoomed', async ({
  mount,
  page,
}) => {
  const { gets, posts } = await setup(page);
  await mount(<RunPageHarness />, { hooksConfig: { route } });
  const ticks = page.getByTestId(ids.runAxis).locator('span');
  await expect(ticks).toHaveCount(6);
  const whole = await ticks.allTextContents();
  await page.getByTestId(ids.runOverviewTurn).first().click();
  await expect(page.getByTestId(ids.runOverview)).toHaveAttribute('data-zoom-turn', '1');
  // Turn 1 ends at 70.5% of 85 s, so the axis ends near 60 s instead of 85 s.
  await expect.poll(async () => ticks.allTextContents()).not.toEqual(whole);
  await expect(page.getByTestId(ids.runTurn)).toHaveCount(1);
  expect(gets).toHaveLength(1);
  await expect.poll(() => posts.includes('zoomed')).toBe(true);
  await page.getByTestId(ids.runWholeRun).click();
  await expect(page.getByTestId(ids.runTurn)).toHaveCount(2);
  await expect.poll(async () => ticks.allTextContents()).toEqual(whole);
});

test('RunPage_WhenErrorsOnlyOn_ShouldAskTheServerShowTwoRowsAndPostErrorsOnly', async ({
  mount,
  page,
}) => {
  const { gets, posts } = await setup(page);
  await mount(<RunPageHarness />, { hooksConfig: { route } });
  await expect(rows(page)).toHaveCount(3);
  await page.getByTestId(ids.runErrorsOnly).click();
  await expect(rows(page)).toHaveCount(2);
  await expect(page.getByTestId(ids.runRowError)).toHaveCount(2);
  expect(gets).toEqual(['', '?errorsOnly=true']);
  await expect.poll(() => posts.includes('errorsOnly')).toBe(true);
  await page.getByTestId(ids.runAllRows).click();
  await expect(rows(page)).toHaveCount(3);
});

test('RunPage_WhenFindTyped_ShouldKeepOnlyMatchingRowsWithoutFetching', async ({ mount, page }) => {
  const { gets } = await setup(page);
  await mount(<RunPageHarness />, { hooksConfig: { route } });
  await page.getByTestId(ids.runFind).fill('edit');
  await expect(rows(page)).toHaveCount(1);
  await expect(rows(page).first()).toContainText('Edit');
  expect(gets).toHaveLength(1);
});

test('RunPage_WhenRowSelected_ShouldShowThePanelAndKeepItInTheAddress', async ({ mount, page }) => {
  await setup(page);
  await mount(<RunPageHarness />, { hooksConfig: { route: `${route}?row=0000000000001005` } });
  await expect(page.getByTestId(ids.runPanelFilePath)).toHaveText('src/App/Foo.cs');
});

test('RunPage_WhenLogOnly_ShouldRenderTheEventTimelineNotTheWaterfall', async ({ mount, page }) => {
  await setup(page);
  await page.route('**/api/inspection/projects/*/items/*/runs/sess-cu-1', (r) =>
    r.fulfill({ json: { ...claudeRunPage, logOnly: true } }),
  );
  await mount(<RunPageHarness runId="sess-cu-1" />, {
    hooksConfig: { route: '/p/SHIP/items/SHIP-1/runs/sess-cu-1' },
  });
  await expect(page.getByTestId(ids.runEvents)).toBeAttached();
  await expect(page.getByTestId(ids.runWaterfall)).toHaveCount(0);
});

test('RunPage_WhenStoreIsUnreachable_ShouldShowTheProblemText', async ({ mount, page }) => {
  await setup(page, 409);
  await mount(<RunPageHarness />, { hooksConfig: { route } });
  const failed = page.getByTestId(ids.runLoadFailed);
  await expect(failed).toHaveAttribute('data-reason', 'store-unreachable');
  await expect(failed).toContainText('ClickHouse is not reachable: connection refused');
});

test('RunPage_WhenRunFails500_ShouldShowTheGenericFailure', async ({ mount, page }) => {
  await setup(page, 500);
  await mount(<RunPageHarness />, { hooksConfig: { route } });
  const failed = page.getByTestId(ids.runLoadFailed);
  await expect(failed).toBeVisible();
  await expect(failed).not.toHaveAttribute('data-reason', 'store-unreachable');
});

test('RunPage_WhenExpired_ShouldSayNoLongerAvailable', async ({ mount, page }) => {
  await setup(page);
  await page.route('**/api/inspection/projects/*/items/*/runs/sess-old', (r) =>
    r.fulfill({ json: { ...claudeRunPage, expired: true } }),
  );
  await mount(<RunPageHarness runId="sess-old" />, {
    hooksConfig: { route: '/p/SHIP/items/SHIP-1/runs/sess-old' },
  });
  await expect(page.getByTestId(ids.runExpired)).toContainText('no longer available');
  await expect(page.getByTestId(ids.runWaterfall)).toHaveCount(0);
});

test('RunPage_WhenErrorsOnlyToggledWithFindText_ShouldKeepTheFindBoxMounted', async ({
  mount,
  page,
}) => {
  await setup(page);
  // A slow errors-only answer: the page must stay up on the previous data while it is in flight.
  await page.route(
    '**/api/inspection/projects/*/items/*/runs/sess-cc-1?errorsOnly=true',
    async (r) => {
      await new Promise((resolve) => setTimeout(resolve, 600));
      await r.fulfill({ json: errorsPage });
    },
  );
  await mount(<RunPageHarness />, { hooksConfig: { route } });
  const find = page.getByTestId(ids.runFind);
  await find.fill('e');
  await find.evaluate((el) => el.setAttribute('data-mounted-marker', 'same'));
  await page.getByTestId(ids.runErrorsOnly).click();
  await expect(page.getByTestId(ids.runLoading)).toHaveCount(0);
  await expect(find).toHaveAttribute('data-mounted-marker', 'same');
  await expect(find).toHaveValue('e');
  await expect(rows(page)).toHaveCount(1);
  await expect(find).toHaveAttribute('data-mounted-marker', 'same');
});
