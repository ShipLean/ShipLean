import { expect, test } from '@playwright/experimental-ct-react';
import type { MeDto } from '../../../../api/me';
import { meDto } from '../../../shell/shellFixtures';
import { quickSearchTestIds as ids } from '../../testIds';
import { recentHarnessIds } from './ct/harnessIds';
import { RecentHarness } from './ct/RecentHarness';

// The part of Playwright's Page and Route this spec uses (the CT package does not re-export the types).
interface StubRoute {
  fulfill(response: { status: number; contentType: string; body: string }): Promise<void>;
}
interface Page {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
  evaluate(script: (arg: unknown) => void, arg?: unknown): Promise<unknown>;
}

const me: MeDto = meDto();
const storageKey = `shiplean.quickSearch.recent.${me.id}`;

const stored = (n: number) => ({
  key: `SHIP-${n}`,
  title: `Opened item ${n}`,
  type: 'story',
  state: 'Active',
  stateCategory: 'inProgress',
  parentPath: [],
  projectKey: 'SHIP',
});

async function setUp(page: Page, entries: unknown[] | null) {
  await page.route('**/api/me', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(me),
    }),
  );
  await page.evaluate(
    (arg) => {
      window.localStorage.removeItem((arg as { key: string }).key);
    },
    { key: storageKey },
  );
  if (entries !== null) {
    await page.evaluate(
      (arg) => {
        const { key, value } = arg as { key: string; value: string };
        window.localStorage.setItem(key, value);
      },
      { key: storageKey, value: JSON.stringify(entries) },
    );
  }
}

test('Recent_WhenNothingTyped_ShouldListTheLastFiveOpened', async ({ mount, page }) => {
  await setUp(page, [1, 2, 3, 4, 5, 6, 7].map(stored));
  await mount(<RecentHarness />);
  await expect(page.getByTestId(recentHarnessIds.userId)).toHaveText(me.id);

  // Fails if the source shows all stored items instead of the first five.
  await expect(page.getByTestId(ids.option)).toHaveCount(5);
  await expect(page.getByTestId(ids.option).first()).toHaveAttribute('data-key', 'SHIP-1');
  await expect(page.getByTestId(ids.option).last()).toHaveAttribute('data-key', 'SHIP-5');
  await expect(page.getByTestId(ids.group)).toHaveCount(1);
  await expect(page.getByTestId(ids.note)).toHaveText('Recent items are kept in this browser');
});

test('Recent_WhenNoneYet_ShouldShowNoRecentGroup', async ({ mount, page }) => {
  await setUp(page, null);
  await mount(<RecentHarness />);
  await expect(page.getByTestId(recentHarnessIds.userId)).toHaveText(me.id);

  // Fails if an empty Recent group is returned.
  await expect(page.getByTestId(ids.dialog)).toBeVisible();
  await expect(page.getByTestId(ids.note)).toHaveText('Recent items are kept in this browser');
  await expect(page.getByTestId(ids.group)).toHaveCount(0);
});

test('Recent_WhenStorageIsBlocked_ShouldStillOpenTheDialog', async ({ mount, page }) => {
  await setUp(page, null);
  await page.evaluate(() => {
    Object.defineProperty(window, 'localStorage', {
      get() {
        throw new DOMException('blocked', 'SecurityError');
      },
    });
  });
  await mount(<RecentHarness />);
  await expect(page.getByTestId(recentHarnessIds.userId)).toHaveText(me.id);

  // Fails if readRecent lets a storage error escape.
  await expect(page.getByTestId(ids.dialog)).toBeVisible();
  await expect(page.getByTestId(ids.group)).toHaveCount(0);
});
