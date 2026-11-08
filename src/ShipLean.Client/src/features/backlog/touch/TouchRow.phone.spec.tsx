import { expect, test } from '@playwright/experimental-ct-react';
import { TouchRowHarness } from '../ct/TouchRowHarness';
import { itemRow, ROWS } from '../ct/fixtures';
import { backlogTestIds as ids } from '../testIds';

// The Stories touch row and list on a phone (SHIP-716 D1). Runs in the CT phone project.

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
// The stand-in handle TouchRowHarness renders through renderHandle.
const HARNESS_HANDLE_ID = 'harness-touch-handle';

const LONG_PARENT = itemRow({
  key: 'SHIP-171',
  title: 'Saved cards at checkout',
  sprint: { id: 'sp24', name: 'Sprint 24', status: 'current' },
  parent: {
    id: 'f2',
    key: 'SHIP-95',
    type: { id: 'feature', name: 'Feature' },
    title: 'Checkout with saved payment methods for returning customers on every device',
  },
});

test('TouchRow_WhenNoEstimate_ShouldShowWarnQuestionMark', async ({ mount, page }) => {
  await mount(<TouchRowHarness rows={ROWS} />, route);

  const unestimated = page.locator(`[data-testid="${ids.touchRow}"][data-key="SHIP-160"]`);
  const points = unestimated.getByTestId(ids.points);
  await expect(points).toHaveText('?');
  await expect(points).toHaveClass(/text-caution/);
  await expect(
    page.locator(`[data-testid="${ids.touchRow}"][data-key="SHIP-158"]`).getByTestId(ids.points),
  ).toHaveText('5');
});

test('TouchRow_WhenParentLong_ShouldWrapToOwnLine', async ({ mount, page }) => {
  await mount(<TouchRowHarness rows={[LONG_PARENT]} />, route);

  const row = page.getByTestId(ids.touchRow);
  const key = await row.getByTestId(ids.key).boundingBox();
  const parent = row.getByTestId(ids.parent);
  const box = await parent.boundingBox();
  // Below the key, and its whole title shows (wrapped, never cut).
  expect(box!.y).toBeGreaterThan(key!.y + key!.height - 1);
  await expect(parent).toContainText(
    'Checkout with saved payment methods for returning customers on every device',
  );
  const cut = await parent.evaluate(
    (element) =>
      element.scrollWidth > element.clientWidth ||
      element.getBoundingClientRect().right > document.documentElement.clientWidth,
  );
  expect(cut).toBe(false);
});

test('TouchRowList_WhenSelecting_ShouldShowCheckboxesInTheHandleSlot', async ({
  mount,
  page,
}) => {
  await mount(<TouchRowHarness rows={ROWS} withHandle selecting />, route);

  const slots = page.getByTestId(ids.touchRowHandle);
  await expect(slots.getByTestId(ids.touchRowSelect)).toHaveCount(ROWS.length);
  await expect(page.getByTestId(HARNESS_HANDLE_ID)).toHaveCount(0);
  await page.getByTestId(ids.touchRow).first().tap();
  await expect(page.getByTestId(ids.touchRow).first()).toHaveAttribute('data-selected', 'true');
  await expect(slots.getByTestId(ids.touchRowSelect).first()).toBeChecked();
});

test('TouchRowList_WhenRenderHandleGiven_ShouldPutItInTheSlot', async ({ mount, page }) => {
  await mount(<TouchRowHarness rows={ROWS} withHandle />, route);

  const slots = page.getByTestId(ids.touchRowHandle);
  await expect(slots.getByTestId(HARNESS_HANDLE_ID)).toHaveCount(ROWS.length);
  await expect(slots.getByTestId(ids.rank)).toHaveCount(0);
});

test('TouchRowList_WhenFiltering_ShouldNotRenderHandles', async ({ mount, page }) => {
  await mount(<TouchRowHarness rows={ROWS} withHandle handles={false} />, route);

  const slots = page.getByTestId(ids.touchRowHandle);
  await expect(slots.getByTestId(ids.rank)).toHaveText(['3', '7']);
  await expect(page.getByTestId(HARNESS_HANDLE_ID)).toHaveCount(0);
});

test('TouchRow_ShouldCarryItsDropTarget', async ({ mount, page }) => {
  await mount(<TouchRowHarness rows={ROWS} />, route);

  await expect(page.getByTestId(ids.touchRow)).toHaveCount(ROWS.length);
  for (const row of ROWS) {
    await expect(page.locator(`[data-drop-target="${row.id}"]`)).toHaveAttribute(
      'data-key',
      row.key,
    );
  }
});
