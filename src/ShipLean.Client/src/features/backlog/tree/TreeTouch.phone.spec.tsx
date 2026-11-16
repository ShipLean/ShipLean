import { expect, test } from '@playwright/experimental-ct-react';
import { PROJECT, treeRows, treeView } from '../ct/fixtures';
import { stubBacklog } from '../ct/stubApi';
import { TreeSelectHarness } from '../ct/TreeSelectHarness';
import { backlogTestIds as ids } from '../testIds';
import { TreeMode } from './TreeMode';

// The Stories tree at 900px and below (SHIP-716 D6): path headers, rows that never indent, and a row lifted by its handle
// over a group header. Real touch input over CDP. Runs in the CT phone project.

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];
interface Point {
  x: number;
  y: number;
}

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=tree' } };
const { rows } = treeRows();
const view = treeView(rows);
const rowOf = (page: Page, key: string) =>
  page.locator(`[data-testid="${ids.row}"][data-key="${key}"]`);
const groupOf = (page: Page, key: string) =>
  page.locator(`[data-testid="${ids.treeGroup}"][data-key="${key}"]`);

async function touchInput(page: Page) {
  const cdp = await page.context().newCDPSession(page);
  const send = (type: 'touchStart' | 'touchMove' | 'touchEnd', point?: Point) =>
    cdp.send('Input.dispatchTouchEvent', { type, touchPoints: point ? [point] : [] });
  return {
    start: (point: Point) => send('touchStart', point),
    async move(from: Point, to: Point) {
      const steps = Math.max(1, Math.ceil(Math.hypot(to.x - from.x, to.y - from.y) / 20));
      for (let i = 1; i <= steps; i++) {
        await send('touchMove', {
          x: from.x + ((to.x - from.x) * i) / steps,
          y: from.y + ((to.y - from.y) * i) / steps,
        });
      }
    },
    end: () => send('touchEnd'),
  };
}

async function centre(locator: ReturnType<typeof rowOf>): Promise<Point> {
  const box = await locator.boundingBox();
  if (!box) throw new Error('no box');
  return { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2) };
}

test('TreeGroupRow_WhenTouch_ShouldShowPath', async ({ mount, page }) => {
  await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);

  await expect(page.locator('table')).toHaveCount(0);
  await expect(groupOf(page, 'SHIP-90').getByTestId(ids.treeGroupPath)).toHaveCount(0);
  await expect(groupOf(page, 'SHIP-90').getByTestId(ids.title)).toHaveText('Storefront');
  await expect(groupOf(page, 'SHIP-101').getByTestId(ids.treeGroupPath)).toHaveText('Storefront ›');
  await expect(groupOf(page, 'SHIP-110').getByTestId(ids.treeGroupPath)).toHaveText(
    'Storefront › Checkout v2 ›',
  );
  await expect(groupOf(page, 'SHIP-110').getByTestId(ids.title)).toHaveText('Receipts');
  await expect(page.getByTestId(ids.treeGroup).last().getByTestId(ids.treeGroupPath)).toHaveCount(
    0,
  );
  await expect(page.getByTestId(ids.treeGroup).last().getByTestId(ids.title)).toHaveText(
    'No parent',
  );
  const caret = await groupOf(page, 'SHIP-101').getByTestId(ids.treeCaret).boundingBox();
  expect(caret!.width).toBeGreaterThanOrEqual(32);
  expect(caret!.height).toBeGreaterThanOrEqual(32);
});

test('TreeItemRow_WhenTouch_ShouldNotIndent', async ({ mount, page }) => {
  await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);

  // SHIP-170 is at depth 1, SHIP-141 at 2 and SHIP-150 at 3.
  const lefts = await Promise.all(
    ['SHIP-170', 'SHIP-141', 'SHIP-150'].map(
      async (key) => (await rowOf(page, key).boundingBox())!.x,
    ),
  );
  expect(new Set(lefts).size).toBe(1);
  const titles = await Promise.all(
    ['SHIP-170', 'SHIP-141', 'SHIP-150'].map(
      async (key) => (await rowOf(page, key).getByTestId(ids.title).boundingBox())!.x,
    ),
  );
  expect(new Set(titles).size).toBe(1);
});

test('TouchTree_WhenHandleOverAGroupHeader_ShouldLightIt', async ({ mount, page }) => {
  const server = await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);
  await expect(rowOf(page, 'SHIP-141')).toBeVisible();
  // The clock is paused once the backlog has loaded: the hold is then moved by hand.
  await page.clock.install({ time: 0 });
  await page.clock.pauseAt(1000);
  const touch = await touchInput(page);
  const target = groupOf(page, 'SHIP-102');
  const from = await centre(rowOf(page, 'SHIP-141').getByTestId(ids.touchHandle));
  const to = await centre(target);

  await touch.start(from);
  await page.clock.runFor(450);
  await touch.move(from, to);
  await expect(target).toHaveAttribute('data-over', 'true');
  await expect(groupOf(page, 'SHIP-101')).toHaveAttribute('data-over', 'false');
  await touch.end();

  await expect(target).toHaveAttribute('data-over', 'false');
  await expect.poll(() => server.patches.length).toBe(1);
});

test('TouchTree_WhenFiltering_ShouldNotRenderHandles', async ({ mount, page }) => {
  await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);
  await expect(page.getByTestId(ids.touchHandle)).toHaveCount(6);

  await page.getByTestId(ids.filterButton).tap();

  await expect(page.getByTestId(ids.touchHandle)).toHaveCount(0);
  await expect(rowOf(page, 'SHIP-141').getByTestId(ids.rank)).toHaveText('1');
});

test('TouchTree_WhenNotFiltering_ShouldRenderAHandlePerRow', async ({ mount, page }) => {
  await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);

  await expect(page.getByTestId(ids.touchHandle)).toHaveCount(6);
});

test('TouchTree_WhenSelecting_ShouldNotRenderHandles', async ({ mount, page }) => {
  await stubBacklog(page, view);
  await mount(<TreeSelectHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.touchHandle)).toHaveCount(6);

  await page.getByTestId('harness-select').tap();

  await expect(page.getByTestId(ids.touchHandle)).toHaveCount(0);
  await expect(rowOf(page, 'SHIP-141').getByTestId(ids.rank)).toHaveText('1');
});

// Catches: putting the path back on the title's line (the path box then isn't above the key and title) or dropping text-ink-muted.
test('TreeGroupHeader_ShouldPutThePathOnAMutedFirstLine', async ({ mount, page }) => {
  await stubBacklog(page, view);
  await mount(<TreeMode project={PROJECT} />, route);

  const group = groupOf(page, 'SHIP-110');
  const path = group.getByTestId(ids.treeGroupPath);
  await expect(path).toHaveClass(/text-ink-muted/);
  await expect(group.getByTestId(ids.key)).toHaveText('SHIP-110');
  const pathBox = await path.boundingBox();
  const keyBox = await group.getByTestId(ids.key).boundingBox();
  const titleBox = await group.getByTestId(ids.title).boundingBox();
  expect(pathBox!.y + pathBox!.height).toBeLessThanOrEqual(keyBox!.y);
  expect(pathBox!.y + pathBox!.height).toBeLessThanOrEqual(titleBox!.y);
});
