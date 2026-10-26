import { expect, test } from '@playwright/experimental-ct-react';
import fixture from '../../../../../ShipLean.TestSupport/DtoFixtures/search/search-view.json' with { type: 'json' };
import { stubShell } from '../../shell/shellFixtures';
import { quickSearchTestIds as ids } from '../testIds';
import { searchSourceHarnessIds as harness } from './ct/harnessIds';
import { SearchSourceHarness } from './ct/SearchSourceHarness';

const route = { hooksConfig: { route: '/p/SHIP/boards/stories' } };
const json = (body: unknown) => ({
  status: 200,
  contentType: 'application/json',
  body: JSON.stringify(body),
});
const forQuery = (q: string) => ({
  ...fixture,
  query: q,
  items: fixture.items.filter((item) => item.key === 'SHIP-12'),
});

interface SearchRoute {
  request(): { url(): string };
  fulfill(answer: { status: number; contentType: string; body: string }): Promise<void>;
}
interface SearchPage {
  route(url: string, handler: (route: SearchRoute) => Promise<void>): Promise<unknown>;
}

async function stubSearch(page: SearchPage, answer = json(fixture)) {
  const urls: string[] = [];
  await page.route('**/api/search*', async (r) => {
    urls.push(r.request().url());
    await r.fulfill(answer);
  });
  return urls;
}

test('SearchResults_WhenWordTyped_ShouldShowGroupedResults', async ({ mount, page }) => {
  await stubShell(page);
  await stubSearch(page);
  await mount(<SearchSourceHarness />, route);
  await page.getByTestId(ids.input).fill('export');
  await expect(page.getByTestId(ids.group)).toHaveText(['Work items', 'Sprints', 'Projects']);
  await expect(page.locator(`[data-testid="${ids.option}"][data-kind="item"]`)).toHaveCount(4);
  await expect(page.locator(`[data-testid="${ids.option}"][data-key="PAY"]`)).toContainText(
    '3 members',
  );
  await expect(page.getByTestId(ids.note)).toHaveText('Top 8 items · keep typing to narrow');
  await expect(page.getByTestId(ids.where).first()).toBeVisible();
});

test('SearchResults_WhenTypingFast_ShouldSendOneRequest', async ({ mount, page }) => {
  await stubShell(page);
  const urls = await stubSearch(page);
  await mount(<SearchSourceHarness />, route);
  await page.getByTestId(ids.input).pressSequentially('export', { delay: 20 });
  await expect(page.getByTestId(ids.list)).toContainText('Export a board to CSV');
  expect(urls).toHaveLength(1);
});

test('SearchResults_WhenAnOlderAnswerArrivesLate_ShouldKeepTheNewerResults', async ({
  mount,
  page,
}) => {
  await stubShell(page);
  let release: () => void = () => undefined;
  const held = new Promise<void>((resolve) => (release = resolve));
  let calls = 0;
  await page.route('**/api/search*', async (r) => {
    const q = new URL(r.request().url()).searchParams.get('q') ?? '';
    calls += 1;
    if (calls === 1) {
      await held;
      // The first request may already be aborted by the time it is released.
      await r
        .fulfill(
          json({ ...fixture, query: q, items: fixture.items.filter((i) => i.key === 'PAY-5') }),
        )
        .catch(() => undefined);
      return;
    }
    await r.fulfill(json(forQuery(q)));
  });
  await mount(<SearchSourceHarness />, route);
  await page.getByTestId(ids.input).fill('exp');
  await expect.poll(() => calls).toBe(1);
  await page.getByTestId(ids.input).fill('export');
  await expect(page.getByTestId(ids.list)).toContainText('Export a board to CSV');
  release();
  await page.waitForTimeout(300);
  // Fails when the late answer replaces the newer one: PAY-5 would show.
  await expect(page.locator(`[data-testid="${ids.option}"][data-key="PAY-5"]`)).toHaveCount(0);
  await expect(page.locator(`[data-testid="${ids.option}"][data-key="SHIP-12"]`)).toHaveCount(1);
});

test('SearchResults_WhenSearching_ShouldKeepThePreviousResultsDimmed', async ({ mount, page }) => {
  await stubShell(page);
  let release: () => void = () => undefined;
  const held = new Promise<void>((resolve) => (release = resolve));
  let calls = 0;
  await page.route('**/api/search*', async (r) => {
    calls += 1;
    if (calls === 2) await held;
    await r.fulfill(json(fixture));
  });
  await mount(<SearchSourceHarness />, route);
  await page.getByTestId(ids.input).fill('export');
  await expect(page.getByTestId(ids.list)).toContainText('Export a board to CSV');
  await page.getByTestId(ids.input).fill('exports');
  await expect(page.getByTestId(ids.loading)).toBeVisible();
  // Fails when the results are cleared while the next query runs.
  await expect(page.getByTestId(ids.list)).toContainText('Export a board to CSV');
  await expect(page.getByTestId(ids.list).locator('..')).toHaveCSS('opacity', '0.6');
  release();
  await expect(page.getByTestId(ids.loading)).toHaveCount(0);
});

test('SearchResults_WhenTheSearchFails_ShouldOfferRetryThatWorks', async ({ mount, page }) => {
  await stubShell(page);
  let calls = 0;
  await page.route('**/api/search*', (r) => {
    calls += 1;
    if (calls === 1) {
      return r.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Boom.', code: 'server' }),
      });
    }
    return r.fulfill(json(fixture));
  });
  await mount(<SearchSourceHarness />, route);
  await page.getByTestId(ids.input).fill('export');
  await expect(page.getByTestId(ids.error)).toBeVisible();
  await page.getByTestId(ids.retry).click();
  await expect(page.getByTestId(ids.error)).toHaveCount(0);
  await expect(page.getByTestId(ids.list)).toContainText('Export a board to CSV');
  expect(calls).toBe(2);
});

test('SearchResults_WhenNothingMatches_ShouldSayWhatWasSearched', async ({ mount, page }) => {
  await stubShell(page);
  await stubSearch(page, json({ ...fixture, projects: [], sprints: [], items: [], more: false }));
  await mount(<SearchSourceHarness />, route);
  await page.getByTestId(ids.input).fill('zzz');
  await expect(page.getByTestId(ids.none)).toContainText('Nothing matches “zzz”');
  await expect(page.getByTestId(ids.note)).toHaveText('All your projects · SHIP first');
});

test('SearchResults_WhenSprintPicked_ShouldNavigateToItsTaskboard', async ({ mount, page }) => {
  await stubShell(page);
  await stubSearch(page);
  await mount(<SearchSourceHarness />, route);
  await page.getByTestId(ids.input).fill('export');
  await page.getByText('Sprint 24').click();
  await expect(page.getByTestId(harness.location)).toHaveText(
    '/p/SHIP/sprints/00000004-0000-4000-8001-000000000019/taskboard',
  );
});

test('SearchResults_WhenQuerySent_ShouldAskForPrefixAndClosedItems', async ({ mount, page }) => {
  await stubShell(page);
  const urls = await stubSearch(page);
  await mount(<SearchSourceHarness />, route);
  await page.getByTestId(ids.input).fill('  export ');
  await expect(page.getByTestId(ids.list)).toContainText('Export a board to CSV');
  const params = new URL(urls[0]).searchParams;
  expect(Object.fromEntries(params)).toEqual({
    q: 'export',
    prefix: 'true',
    closed: 'true',
    limit: '8',
  });
});
