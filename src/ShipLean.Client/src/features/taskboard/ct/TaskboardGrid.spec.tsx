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
