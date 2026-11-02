import { expect, test } from '@playwright/experimental-ct-react';
import type { Locator, Page } from 'playwright';
import { ListGroupHarness } from '../ct/ListGroupHarness';
import { backlogView, itemRow, PROJECT } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { backlogTestIds as ids } from '../testIds';

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const ROWS = [1, 2, 3, 4, 5].map((n) => itemRow({ key: `SHIP-${n}`, rank: n, revision: n }));
const rowOf = (page: Page, key: string) =>
  page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
// Picks `row` up by its grip and puts it down on `target`. The button is released once the sortable list has made
// room at the target (it slid), which is how dnd-kit shows it knows the drop.
async function dragRow(
  page: Page,
  row: Locator,
  target: Locator,
  beforeDrop: () => Promise<void> = async () => undefined,
) {
  const grip = (await row.getByTestId(ids.grip).boundingBox())!;
  const box = (await target.boundingBox())!;
  await page.mouse.move(grip.x + grip.width / 2, grip.y + grip.height / 2);
  await page.mouse.down();
  await page.mouse.move(grip.x + 5, grip.y + 20, { steps: 4 });
  await page.mouse.move(grip.x + 5, box.y + box.height / 2, { steps: 10 });
  await expect(target).toHaveAttribute('style', /translate3d\(0px, -?[1-9]/);
  await beforeDrop();
  await page.mouse.up();
}
const select = async (page: Page, ...keys: string[]) => {
  for (const key of keys) await page.getByTestId(`harness-select-${key}`).click();
};

test('List_WhenASelectedRowIsDragged_ShouldMoveTheBlockAboveTheTargetInRankOrder', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListGroupHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(5);
  await select(page, 'SHIP-3', 'SHIP-1');

  await dragRow(page, rowOf(page, 'SHIP-3'), rowOf(page, 'SHIP-5'));

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items.map((i) => [i.id, i.placement])).toEqual([
    ['id-SHIP-1', { slot: 'before', beforeItemId: 'id-SHIP-5' }],
    ['id-SHIP-3', { slot: 'before', beforeItemId: 'id-SHIP-5' }],
  ]);
  expect(server.reorders).toHaveLength(0);
});

test('List_WhenASelectedRowIsHeld_ShouldShowTheBadgeAndDimTheRest', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListGroupHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(5);
  await select(page, 'SHIP-1', 'SHIP-2');

  const grip = (await rowOf(page, 'SHIP-1').getByTestId(ids.grip).boundingBox())!;
  const x = grip.x + grip.width / 2;
  const y = grip.y + grip.height / 2;
  await page.mouse.move(x, y);
  await page.mouse.down();
  await page.mouse.move(x + 5, y + 30, { steps: 6 });

  await expect(page.getByTestId(ids.dragBadge)).toHaveText('2 items');
  await expect(rowOf(page, 'SHIP-2')).toHaveCSS('opacity', '0.4');
  await page.keyboard.press('Escape');
  await page.mouse.up();
  expect(server.bulks).toHaveLength(0);
});

test('List_WhenDroppedOnASelectedRow_ShouldSendNothing', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListGroupHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(5);
  await select(page, 'SHIP-1', 'SHIP-2');

  await dragRow(page, rowOf(page, 'SHIP-1'), rowOf(page, 'SHIP-2'));

  await expect(page.getByTestId(ids.dragBadge)).toHaveCount(0);
  expect(server.bulks).toHaveLength(0);
  expect(server.reorders).toHaveLength(0);
  await expect(page.getByTestId('toast')).toHaveCount(0);
});

test('List_WhenTheSelectionChangesMidDragAndTheDropIsOnADraggedRow_ShouldSendNothing', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListGroupHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(5);
  await select(page, 'SHIP-1', 'SHIP-2');

  // SHIP-2 leaves the live selection while the block of SHIP-1 and SHIP-2 is still in the air.
  await dragRow(page, rowOf(page, 'SHIP-1'), rowOf(page, 'SHIP-2'), () =>
    page.getByTestId('harness-select-SHIP-2').dispatchEvent('pointerdown'),
  );

  await expect(page.getByTestId(ids.dragBadge)).toHaveCount(0);
  expect(server.bulks).toHaveLength(0);
  expect(server.reorders).toHaveLength(0);
});

test('List_WhenAnUnselectedRowIsDragged_ShouldReorderOnlyIt', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<ListGroupHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(5);
  await select(page, 'SHIP-1', 'SHIP-2');

  await dragRow(page, rowOf(page, 'SHIP-4'), rowOf(page, 'SHIP-3'));

  expect(server.bulks).toHaveLength(0);
});
