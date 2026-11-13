import { expect, test } from '@playwright/experimental-ct-react';
import type { LinkDto } from '@shiplean/core/api/workItems';
import { ItemPeekHarness } from '../../features/itemPeek/ct/ItemPeekHarness';
import { itemPeekTestIds } from '../../features/itemPeek/testIds';
import { mei, members, priya, sprints, states, story } from '../../features/workItem/ct/fixtures';
import { SideRailHarness } from '../../features/workItem/ct/SideRailHarness';
import { stubApi } from '../../features/workItem/ct/stubApi';
import { attachment, change, revision, stubSide } from '../../features/workItem/ct/stubSide';
import { WorkItemFormHarness } from '../../features/workItem/ct/WorkItemFormHarness';
import { workItemTestIds as ids } from '../../features/workItem/testIds';
import { touchTestIds } from '../../touch/testIds';
import { stubProjectApi } from '../pagesFixtures';
import type { Page } from './shoot';

// The work item's Links, Attachments and History on a phone and the item modal's bar (SHIP-714, L714d), as element shots
// of this task's regions only, so the other 714 tasks' merges don't move them.
// Regenerate with scripts/update-page-shots.sh src/pages/screenshots/itemSide.phone.spec.tsx.

const THEMES = ['light', 'dark'] as const;
const route = (path: string) => ({ hooksConfig: { route: path } });

const links: LinkDto[] = [
  {
    id: 'l1',
    type: 'related',
    workItemId: 'w144',
    key: 'SHIP-144',
    title: 'Saved cards list on the account page',
    url: null,
    card: null,
    autoLinked: null,
  },
  {
    id: 'l2',
    type: 'url',
    workItemId: null,
    key: null,
    title: 'SHIP-141 Saved card picker',
    url: 'https://github.com/acme/storefront/pull/418',
    card: {
      state: 'merged',
      title: 'SHIP-141 Saved card picker',
      asOf: '2026-10-25T09:30:00+00:00',
      asOfLabel: '4 min ago',
      source: 'github',
    },
    autoLinked: { via: 'github', matchedOn: 'branch' },
  },
];
const files = [
  attachment('a1', 'saved-card-picker-mockup-final-v3.png', mei, 421888),
  attachment('a2', 'contract.pdf', null, 1258291),
];
const revisions = [5, 4, 3, 2, 1].map((rev) =>
  revision(rev, rev % 2 ? mei : priya, [change('points', rev, rev + 1)]),
);

const phoneOnly = (page: Page & { viewportSize(): { width: number } | null }) =>
  expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);

interface StubRoute {
  request(): { url(): string };
  fulfill(response: { status: number; contentType: string; body: string }): Promise<void>;
}
async function stubPeek(page: {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
}) {
  await stubProjectApi(page);
  const json = (body: unknown) => ({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
  await page.route('**/api/projects/id-SHIP/work-items/by-key/*', (r) =>
    r.fulfill(json({ ...story, projectId: 'id-SHIP' })),
  );
  await page.route('**/api/projects/id-SHIP/states', (r) => r.fulfill(json(states)));
  await page.route('**/api/projects/id-SHIP/members', (r) => r.fulfill(json(members)));
  await page.route('**/api/projects/id-SHIP/sprints', (r) => r.fulfill(json(sprints)));
  await page.route('**/api/projects/id-SHIP/work-items?*', (r) => r.fulfill(json([])));
  await page.route('**/api/work-items/*/comments', (r) => r.fulfill(json([])));
  await page.route('**/api/markdown/render', (r) =>
    r.fulfill(json({ markdown: '', html: '', images: [] })),
  );
}

for (const theme of THEMES) {
  test(`ItemSidePhone_WhenLinked_ShouldMatchItsScreenshot_item-links_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubApi(page, { ...story, links });
    const screen = await mount(
      <WorkItemFormHarness itemKey="SHIP-141" realLinks />,
      route('/p/SHIP/items/SHIP-141'),
    );
    await expect(screen.getByTestId(ids.linkRow)).toHaveCount(2);

    await expect(screen.getByTestId(ids.links)).toHaveScreenshot(`item-links.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`ItemSidePhone_WhenAttached_ShouldMatchItsScreenshot_item-attachments_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubApi(page, story);
    await stubSide(page, mei, files, revisions);
    const screen = await mount(
      <SideRailHarness itemKey="SHIP-141" />,
      route('/p/SHIP/items/SHIP-141'),
    );
    await expect(screen.getByTestId(ids.attachment)).toHaveCount(2);

    await expect(screen.getByTestId(ids.attachments)).toHaveScreenshot(
      `item-attachments.${theme}.png`,
      { animations: 'disabled' },
    );
  });

  test(`ItemSidePhone_WhenLongHistory_ShouldMatchItsScreenshot_item-history_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubApi(page, story);
    await stubSide(page, mei, [], revisions);
    const screen = await mount(
      <SideRailHarness itemKey="SHIP-141" />,
      route('/p/SHIP/items/SHIP-141'),
    );
    await expect(screen.getByTestId(ids.revision)).toHaveCount(3);
    await expect(screen.getByTestId(ids.historyShowAll)).toBeVisible();

    await expect(screen.getByTestId(ids.history)).toHaveScreenshot(`item-history.${theme}.png`, {
      animations: 'disabled',
    });
  });

  test(`ItemSidePhone_WhenModalOpen_ShouldMatchItsScreenshot_item-modal-bar_${theme}`, async ({
    mount,
    page,
  }) => {
    phoneOnly(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stubPeek(page);
    await mount(
      <ItemPeekHarness itemKey="SHIP-141" />,
      route('/p/SHIP/boards/stories?item=SHIP-141'),
    );
    const dialog = page.getByTestId(itemPeekTestIds.dialog);
    await expect(dialog.getByTestId(ids.title)).toBeVisible();

    // The bar is the dialog header, the ✕'s parent.
    const bar = dialog.getByTestId(touchTestIds.dialogClose).locator('..');
    await expect(bar).toHaveScreenshot(`item-modal-bar.${theme}.png`, { animations: 'disabled' });
  });
}
