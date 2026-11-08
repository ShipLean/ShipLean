import { expect, test } from '@playwright/experimental-ct-react';
import { stubProjectApi } from '../../pages/pagesFixtures';
import { touchTestIds } from '../../touch/testIds';
import { members, sprints, states, story } from '../workItem/ct/fixtures';
import { ItemPeekHarness } from './ct/ItemPeekHarness';
import { itemPeekTestIds as ids } from './testIds';

// The item modal on the CT phone project (390×844): K5's full-screen Dialog with ✕ at the top left and ↗ Open full page as
// its primary action (SHIP-714 D8, SHIP-805 C9).

interface StubRoute {
  request(): { url(): string };
  fulfill(response: { status: number; contentType: string; body: string }): Promise<void>;
}
async function stub(page: {
  route(url: string, handler: (route: StubRoute) => Promise<void>): Promise<unknown>;
}) {
  await stubProjectApi(page);
  const json = (body: unknown) => ({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
  await page.route('**/api/projects/id-SHIP/work-items/by-key/*', (route) =>
    route.fulfill(json({ ...story, projectId: 'id-SHIP' })),
  );
  await page.route('**/api/projects/id-SHIP/states', (route) => route.fulfill(json(states)));
  await page.route('**/api/projects/id-SHIP/members', (route) => route.fulfill(json(members)));
  await page.route('**/api/projects/id-SHIP/sprints', (route) => route.fulfill(json(sprints)));
  await page.route('**/api/projects/id-SHIP/work-items?*', (route) => route.fulfill(json([])));
  await page.route('**/api/work-items/*/comments', (route) => route.fulfill(json([])));
  await page.route('**/api/markdown/render', (route) =>
    route.fulfill(json({ markdown: '', html: '', images: [] })),
  );
}

const at = (route: string) => ({ hooksConfig: { route } });

test('ItemPeek_WhenTouch_ShouldFillScreenWithCloseLeftAndOpenFullPageRight', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<ItemPeekHarness itemKey="SHIP-141" />, at('/p/SHIP/sprints?item=SHIP-141'));
  const dialog = page.getByTestId(ids.dialog);
  await expect(dialog).toBeVisible();

  const panel = (await dialog.boundingBox())!;
  // (the CT page's own content can widen the layout viewport, so the panel's size is checked, not its origin.)
  expect(panel).toMatchObject({ width: 390, height: 844 });
  const close = (await dialog.getByTestId(touchTestIds.dialogClose).boundingBox())!;
  const open = dialog.getByTestId(ids.openFullPage);
  const openBox = (await open.boundingBox())!;
  // ✕ first at the top left; the link sits in the primary slot at the top right, both at least 32px.
  expect(close.x).toBeLessThan(openBox.x);
  expect(close.x - panel.x).toBeLessThan(16);
  expect(openBox.x + openBox.width - panel.x).toBeGreaterThan(390 - 16);
  expect(close.height).toBeGreaterThanOrEqual(32);
  expect(openBox.height).toBeGreaterThanOrEqual(32);
  await expect(
    dialog.getByTestId(touchTestIds.dialogPrimary).getByTestId(ids.openFullPage),
  ).toHaveText('↗ Open full page');
  await expect(open).toHaveAttribute('href', '/p/SHIP/items/SHIP-141');
  // Exactly one link and one close: the desktop header's ×.
  await expect(dialog.getByTestId(ids.close)).toHaveCount(0);
  await expect(dialog.getByTestId(ids.openFullPage)).toHaveCount(1);
});

test('ItemPeek_WhenCloseTappedOnTouch_ShouldDropItemFromUrl', async ({ mount, page }) => {
  await stub(page);
  await mount(<ItemPeekHarness itemKey="SHIP-141" />, at('/p/SHIP/sprints?item=SHIP-141'));
  await expect(page.getByTestId(ids.dialog)).toBeVisible();

  await page.getByTestId(touchTestIds.dialogClose).tap();

  await expect(page.getByTestId(ids.dialog)).toHaveCount(0);
  await expect(page.getByTestId(ids.address)).toHaveText('/p/SHIP/sprints');
});
