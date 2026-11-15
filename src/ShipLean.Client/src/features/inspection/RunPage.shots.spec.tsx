import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import { claudeRunPage, stubInspection } from './ct/inspectionMocks';
import { errorsPage } from './page/ct/errorsPage';
import { RunPageHarness } from './page/ct/RunPageHarness';
import { inspectionTestIds as ids } from './testIds';

const base = '/p/SHIP/items/SHIP-1/runs/sess-cc-1';

type State = 'default' | 'zoomed' | 'errors-only' | 'store-unreachable' | 'expired';
const query: Record<State, string> = {
  default: '',
  zoomed: '?turn=1',
  'errors-only': '?errorsOnly=true',
  'store-unreachable': '',
  expired: '',
};

async function setup(page: Page, state: State) {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.emulateMedia({ colorScheme: 'light', reducedMotion: 'reduce' });
  await stubInspection(page);
  await page.route('**/api/projects/by-key/*', (r) =>
    r.fulfill({ json: { id: 'p1', key: 'SHIP', role: 'member' } }),
  );
  await page.route('**/api/inspection/projects/*/items/*/runs/*', (r) => {
    const url = new URL(r.request().url());
    if (/\/rows\//.test(url.pathname)) return r.fallback();
    if (state === 'store-unreachable')
      return r.fulfill({
        status: 409,
        json: { code: 'conflict', error: 'ClickHouse is not reachable: connection refused' },
      });
    if (state === 'expired') return r.fulfill({ json: { ...claudeRunPage, expired: true } });
    return r.fulfill({
      json: url.searchParams.get('errorsOnly') === 'true' ? errorsPage : claudeRunPage,
    });
  });
}

// Each state asserts its own data, so a baseline cannot pass for the wrong state.
const assertState: Record<State, (page: Page) => Promise<void>> = {
  default: async (page) => {
    await expect(page.getByTestId(ids.runTurn)).toHaveCount(2);
    await expect(page.getByTestId(ids.runRow)).toHaveCount(3);
    await expect(page.getByTestId(ids.runRowError)).toHaveCount(0);
    await expect(page.getByTestId(ids.runTotalErrors)).toHaveText('4');
  },
  zoomed: async (page) => {
    await expect(page.getByTestId(ids.runOverview)).toHaveAttribute('data-zoom-turn', '1');
    await expect(page.getByTestId(ids.runTurn)).toHaveCount(1);
    await expect(page.getByTestId(ids.runWholeRun)).toBeVisible();
  },
  'errors-only': async (page) => {
    await expect(page.getByTestId(ids.runRow)).toHaveCount(2);
    await expect(page.getByTestId(ids.runRowError)).toHaveCount(2);
    await expect(page.getByTestId(ids.runRow).first()).toContainText('Bash');
  },
  'store-unreachable': async (page) => {
    await expect(page.getByTestId(ids.runLoadFailed)).toHaveAttribute(
      'data-reason',
      'store-unreachable',
    );
    await expect(page.getByTestId(ids.runLoadFailed)).toContainText('connection refused');
    await expect(page.getByTestId(ids.runRow)).toHaveCount(0);
  },
  expired: async (page) => {
    await expect(page.getByTestId(ids.runExpired)).toContainText('no longer available');
    await expect(page.getByTestId(ids.runRow)).toHaveCount(0);
  },
};

for (const state of Object.keys(query) as State[]) {
  test(`RunPage_WhenState${state}_ShouldMatchItsScreenshot`, async ({ mount, page }) => {
    await setup(page, state);
    const c = await mount(<RunPageHarness />, { hooksConfig: { route: base + query[state] } });
    await assertState[state](page);
    await expect(c).toHaveScreenshot(`run-page-${state}.png`, { animations: 'disabled' });
  });
}
