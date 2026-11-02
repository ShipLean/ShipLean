import { expect, test } from '@playwright/experimental-ct-react';
import { RETRO_CATEGORIES } from '../../../api/retros';
import { sprintRetro, stubRetros } from '../../retros/ct/retroFixtures';
import { retroSharedTestIds as ids } from '../testIds';
import { RetroBoardHarness } from './ct/RetroBoardHarness';

const points = sprintRetro.categories.flatMap((c) => c.groups.flatMap((g) => g.points));

test('RetroBoard_WhenMounted_ShouldRenderColumnsInTheFixedOrder', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroBoardHarness />);
  const columns = page.getByTestId(ids.retroBoardColumn);
  await expect(columns).toHaveCount(4);
  const order = await columns.evaluateAll((els) => els.map((e) => e.getAttribute('data-category')));
  expect(order).toEqual([...RETRO_CATEGORIES]);
});

test('RetroBoard_WhenAFilterHidesEveryNote_ShouldStillShowToDecide', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroBoardHarness hideAll />);
  await expect(page.getByTestId(ids.retroNote)).toHaveCount(0);
  const withOpen = sprintRetro.categories.filter((c) => c.toDecide > 0);
  expect(withOpen.length).toBeGreaterThan(0);
  for (const column of withOpen) {
    await expect(
      page
        .getByTestId(ids.retroBoardColumn)
        .and(page.locator(`[data-category="${column.category}"]`)),
    ).toContainText(`${column.toDecide} to decide`);
  }
});

test('RetroBoard_WhenAPointIsNamed_ShouldHighlightOnlyThatNote', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroBoardHarness highlightedPointId={points[1].id} />);
  const marked = page.locator(`[data-testid="${ids.retroNote}"][data-highlighted="true"]`);
  await expect(marked).toHaveCount(1);
  await expect(marked).toHaveAttribute('data-point-id', points[1].id);
});
