import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { StateDto } from '@shiplean/core/api/projects';
import { touchTestIds } from '../../../touch/testIds';
import { story } from '../../workItem/ct/fixtures';
import { PROJECT, planningManyView, planningSprints } from '../ct/fixtures';
import { DrawerToggle } from '../ct/DrawerToggle';
import { SelectModeHarness } from '../ct/SelectModeHarness';
import { stubBacklog } from '../ct/stubApi';
import { PlanningMode } from '../planning/PlanningMode';
import { backlogTestIds as ids } from '../testIds';

// The bulk bar's State, Points, Parent and Tags sheets, Move to top and bottom, and the bar's layer, on a phone
// (SHIP-716 D3). Runs in the CT phone project.

const planning = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' } };
const state = (id: string, type: StateDto['type'], name: string, order: number): StateDto => ({
  id,
  type,
  name,
  category: 'inProgress',
  order,
  itemCount: 0,
});
const STATES: StateDto[] = [
  state('s-active', 'story', 'Active', 1),
  state('b-active', 'bug', 'Active', 1),
];
const feature = {
  ...story,
  id: 'f1',
  key: 'SHIP-10',
  title: 'Checkout',
  type: { id: 'feature', name: 'Feature' },
};

async function open(
  mount: (node: React.JSX.Element, options?: object) => Promise<unknown>,
  page: Page,
  withDrawer = false,
) {
  const server = await stubBacklog(page, planningManyView(), { sprints: planningSprints() });
  await page.route('**/api/projects/p1/states', (route) =>
    route.fulfill({ contentType: 'application/json', body: JSON.stringify(STATES) }),
  );
  await page.route('**/api/projects/p1/work-items?*', (route) => {
    const type = new URL(route.request().url()).searchParams.get('type');
    return route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(type === 'feature' ? [feature] : []),
    });
  });
  await mount(
    <SelectModeHarness>
      {withDrawer ? <DrawerToggle /> : null}
      <PlanningMode project={PROJECT} />
    </SelectModeHarness>,
    planning,
  );
  await expect(page.getByTestId(ids.row).first()).toBeVisible();
  await page.getByTestId(touchTestIds.moreButton).first().tap();
  await page
    .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="select"]`)
    .tap();
  for (const key of ['SHIP-180', 'SHIP-182']) {
    await page
      .locator(`[data-testid="${ids.row}"][data-key="${key}"]`)
      .getByTestId(ids.touchRowSelect)
      .tap();
  }
  await expect(page.getByTestId(ids.bulkCount)).toHaveText('2 selected');
  return server;
}

const barMore = (page: Page, action: string) =>
  page
    .getByTestId(ids.bulkBar)
    .getByTestId(touchTestIds.moreButton)
    .tap()
    .then(() =>
      page
        .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="${action}"]`)
        .tap(),
    );

test('BulkStateSheet_WhenStatePicked_ShouldSetStateOnEachRowsType', async ({ mount, page }) => {
  const server = await open(mount, page);

  await page.getByTestId(ids.bulkTouchState).tap();
  await page.getByTestId(ids.bulkSheetOption).filter({ hasText: 'Active' }).tap();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => [i.id, i.changes?.state])).toEqual([
    ['id-SHIP-180', { value: 's-active' }],
    ['id-SHIP-182', { value: 'b-active' }],
  ]);
});

test('BulkPointsSheet_WhenSetTapped_ShouldSetPointsOnEveryRow', async ({ mount, page }) => {
  const server = await open(mount, page);

  await page.getByTestId(ids.bulkTouchPoints).tap();
  await page.getByTestId(ids.bulkSheetPointsInput).fill('13');
  await page.getByTestId(ids.bulkSheetPointsSet).tap();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => i.changes?.points)).toEqual([
    { value: 13 },
    { value: 13 },
  ]);
});

test('BulkPointsSheet_WhenClearTapped_ShouldClearPointsOnEveryRow', async ({ mount, page }) => {
  const server = await open(mount, page);

  await page.getByTestId(ids.bulkTouchPoints).tap();
  await page.getByTestId(ids.bulkSheetPointsClear).tap();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => i.changes?.points)).toEqual([
    { value: null },
    { value: null },
  ]);
});

test('BulkTagSheet_WhenTagAdded_ShouldAddTagToEachRow', async ({ mount, page }) => {
  const server = await open(mount, page);

  await barMore(page, 'tag');
  await page.getByTestId(ids.bulkSheetTagInput).fill('urgent');
  await page.getByTestId(ids.bulkSheetTagAdd).tap();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => i.changes?.tags)).toEqual([
    { value: ['urgent'] },
    { value: ['urgent'] },
  ]);
});

test('BulkParentSheet_WhenParentPicked_ShouldSetParentOnEachRow', async ({ mount, page }) => {
  const server = await open(mount, page);

  await barMore(page, 'parent');
  await page.getByTestId(ids.bulkSheetParent).getByText('Checkout').tap();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => i.changes?.parent)).toEqual([
    { value: 'f1' },
    { value: 'f1' },
  ]);
});

for (const [action, slot] of [
  ['top', 'first'],
  ['bottom', 'last'],
] as const) {
  test(`BulkMore_WhenMovedTo${action === 'top' ? 'Top' : 'Bottom'}_ShouldPlaceTheBlockAtThatEdge`, async ({
    mount,
    page,
  }) => {
    const server = await open(mount, page);

    await barMore(page, action);

    await expect.poll(() => server.bulks.length).toBe(1);
    expect(server.bulks[0].items.map((i) => i.id)).toEqual(['id-SHIP-180', 'id-SHIP-182']);
    expect(server.bulks[0].items.map((i) => i.placement)).toEqual([
      { slot, beforeItemId: null },
      { slot, beforeItemId: null },
    ]);
  });
}

test('BulkBar_WhenDrawerOpen_ShouldSitBelowTheDrawerScrim', async ({ mount, page }) => {
  await open(mount, page, true);
  await page.getByTestId('ct-open-drawer').click();
  await expect(page.getByTestId(touchTestIds.drawerScrim)).toBeVisible();

  const bar = await page.getByTestId(ids.bulkBar).boundingBox();
  // What is on top at the bar's middle is the drawer (its panel or its scrim), never the bar.
  const covered = await page.evaluate(
    ({ x, y, barId }) => {
      const top = document.elementFromPoint(x, y);
      return top !== null && top.closest(`[data-testid="${barId}"]`) === null;
    },
    {
      x: (bar?.x ?? 0) + (bar?.width ?? 0) / 2,
      y: (bar?.y ?? 0) + (bar?.height ?? 0) / 2,
      barId: ids.bulkBar,
    },
  );
  expect(covered).toBe(true);
});
