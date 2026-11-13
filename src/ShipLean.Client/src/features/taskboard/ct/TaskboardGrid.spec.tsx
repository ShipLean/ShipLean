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
  await page.setViewportSize({ width: 901, height: 500 });
  await mount(<GridHarness rowIndexes={[0, 1, 2]} scrollerHeight={160} extraColumns={2} />, route);
  const scroller = page.getByTestId('scroller');
  const header = page.getByTestId(ids.columnHeader).first();

  const top = (await scroller.boundingBox())!.y;
  await scroller.evaluate((el) => el.scrollTo({ top: el.scrollHeight }));
  await expect.poll(() => scroller.evaluate((el) => el.scrollTop)).toBeGreaterThan(0);
  await expect.poll(async () => (await header.boundingBox())!.y).toBeCloseTo(top, 0);

  // Narrow: the same scroller scrolls sideways, so no column is lost.
  expect(await scroller.evaluate((el) => el.scrollWidth > el.clientWidth)).toBe(true);
});

test('TaskboardGrid_WhenATaskTitleHasAWordWiderThanTheCard_ShouldWrapItInsideTheCard', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 600, height: 500 });
  const longWord = 'AskOwnerSql_WhenOwnerIsALiveMember_ShouldCoalesceToOrAcrossEveryAgeBoundary';
  await mount(
    <GridHarness rowIndexes={[2]} retitle={{ 'SHIP-182': `Mutation evidence for ${longWord}` }} />,
    route,
  );
  const card = page.getByTestId(ids.card);
  const title = page.getByTestId(ids.cardTitle);

  const cardBox = (await card.boundingBox())!;
  const titleBox = (await title.boundingBox())!;
  expect(titleBox.x + titleBox.width).toBeLessThanOrEqual(cardBox.x + cardBox.width);
  expect(await title.evaluate((el) => el.scrollWidth <= el.clientWidth)).toBe(true);
});

test('TaskboardGrid_WhenParentCrumbIsShort_ShouldKeepWordsWhole', async ({ mount, page }) => {
  await page.setViewportSize({ width: 600, height: 500 });
  await mount(<GridHarness rowIndexes={[2]} peopleFeatureTitle="Reporting" />, route);
  const feature = page.getByTestId(ids.cardParentFeature).first();

  // One line: a Range over the word has a single client rect, and the element is no taller than a line of its text.
  const lines = await feature.evaluate((el) => {
    const range = document.createRange();
    range.selectNodeContents(el);
    return range.getClientRects().length;
  });
  expect(lines).toBe(1);
});
