import { expect, test } from '@playwright/experimental-ct-react';
import { taskboardTestIds as ids } from '../testIds';
import { GridHarness } from './GridHarness';

const route = { hooksConfig: { route: '/p/SHIP/sprints/s24/taskboard' } };

test('TaskboardGrid_WhenGivenASubsetOfRows_ShouldDrawAndCountOnlyThose', async ({
  mount,
  page,
}) => {
  // The second row alone: one task, in Closed.
  await mount(<GridHarness rowIndexes={[1]} />, route);

  await expect(page.getByTestId(ids.row)).toHaveCount(1);
  await expect(page.getByTestId(ids.columnCount)).toHaveText(['0', '0', '1']);
});

test('TaskboardGrid_WhenGivenAllRows_ShouldCountEveryTask', async ({ mount, page }) => {
  await mount(<GridHarness rowIndexes={[0, 1, 2]} />, route);

  await expect(page.getByTestId(ids.columnCount)).toHaveText(['2', '1', '2']);
});

test('TaskboardGrid_WhenItsScrollerScrolls_ShouldKeepTheColumnHeadersAtTheTop', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 600, height: 500 });
  await mount(<GridHarness rowIndexes={[0, 1, 2]} scrollerHeight={160} />, route);
  const scroller = page.getByTestId('scroller');
  const header = page.getByTestId(ids.columnHeader).first();

  const top = (await scroller.boundingBox())!.y;
  await scroller.evaluate((el) => el.scrollTo({ top: el.scrollHeight }));
  await expect.poll(() => scroller.evaluate((el) => el.scrollTop)).toBeGreaterThan(0);
  await expect.poll(async () => (await header.boundingBox())!.y).toBeCloseTo(top, 0);

  // Narrow: the same scroller scrolls sideways, so no column is lost.
  expect(await scroller.evaluate((el) => el.scrollWidth > el.clientWidth)).toBe(true);
});
