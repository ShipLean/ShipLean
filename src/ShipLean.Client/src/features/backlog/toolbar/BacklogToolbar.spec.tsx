import { expect, test } from '@playwright/experimental-ct-react';
import { ToolbarHarness } from '../ct/ToolbarHarness';
import { BacklogToolbar } from './BacklogToolbar';
import { backlogTestIds as ids } from '../testIds';

test('Toolbar_WhenFilterRenderPropGiven_ShouldRenderItInTheFilterSlot', async ({ mount, page }) => {
  await mount(<ToolbarHarness withFilter />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories' },
  });
  await expect(page.getByTestId(ids.filterSlot).getByTestId('harness-filter')).toBeVisible();
});

test('Toolbar_WhenNoFilterRenderProp_ShouldLeaveTheFilterSlotEmpty', async ({ mount, page }) => {
  await mount(<ToolbarHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await expect(page.getByTestId(ids.filterSlot)).toBeEmpty();
});

test('Toolbar_WhenModeClicked_ShouldNavigateToThatMode', async ({ mount, page }) => {
  await mount(<ToolbarHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  const options = page.getByTestId(ids.modeOption);
  await expect(options).toHaveText(['List', 'Planning', 'Tree']);
  await expect(options.nth(0)).toHaveAttribute('aria-pressed', 'true');

  await options.nth(1).click();
  await expect(page.getByTestId('harness-location')).toHaveText(
    '/p/SHIP/backlogs/stories?mode=planning',
  );
  await options.nth(2).click();
  await expect(page.getByTestId('harness-location')).toHaveText(
    '/p/SHIP/backlogs/stories?mode=tree',
  );
});

test('Toolbar_WhenModeIsPlanning_ShouldMarkOnlyPlanningPressed', async ({ mount, page }) => {
  await mount(<ToolbarHarness mode="planning" />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' },
  });
  const options = page.getByTestId(ids.modeOption);
  await expect(options.nth(0)).toHaveAttribute('aria-pressed', 'false');
  await expect(options.nth(1)).toHaveAttribute('aria-pressed', 'true');
});

test('Toolbar_WhenCompletedToggledAndNewItemClicked_ShouldCallBack', async ({ mount, page }) => {
  await mount(<ToolbarHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await page.getByTestId(ids.completedToggle).check();
  await page.getByTestId(ids.newItem).click();
  await expect(page.getByTestId('harness-state')).toHaveText('true|new-clicked');
});

test('Toolbar_WhenColumnsOpened_ShouldListColumnsAndReportChanges', async ({ mount, page }) => {
  await mount(<ToolbarHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await page.getByTestId(ids.columnsButton).click();
  const options = page.getByTestId(ids.columnOption);
  await expect(options).toHaveCount(2);
  await expect(options.nth(0)).toBeDisabled();
  await options.nth(1).uncheck();
  await expect(page.getByTestId('harness-state')).toHaveText('false|state');
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(ids.columnsMenu)).toHaveCount(0);
});

test('Toolbar_WhenNoCompletedPairOrColumns_ShouldHideThoseControls', async ({ mount, page }) => {
  await mount(<ToolbarHarness bare />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await expect(page.getByTestId(ids.newItem)).toBeVisible();
  await expect(page.getByTestId(ids.completedToggle)).toHaveCount(0);
  await expect(page.getByTestId(ids.columnsButton)).toHaveCount(0);
});

test('Toolbar_WhenOnlyOneCompletedPropGiven_ShouldHideTheToggle', async ({ mount, page }) => {
  await mount(
    <BacklogToolbar
      projectKey="SHIP"
      level="stories"
      mode="list"
      onNewItem={() => undefined}
      includeCompleted
    />,
    { hooksConfig: { route: '/p/SHIP/backlogs/stories' } },
  );
  await expect(page.getByTestId(ids.completedToggle)).toHaveCount(0);
});

test('Toolbar_WhenOptionsRenderPropGiven_ShouldRenderItBeforeCompleted', async ({
  mount,
  page,
}) => {
  await mount(<ToolbarHarness withOptions />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories' },
  });
  const option = page.getByTestId('harness-option');
  await expect(option).toBeVisible();
  const order = await page
    .getByTestId(ids.toolbar)
    .locator('[data-testid="harness-option"], [data-testid="backlog-completed-toggle"]')
    .evaluateAll((els) => els.map((e) => e.getAttribute('data-testid')));
  expect(order).toEqual(['harness-option', 'backlog-completed-toggle']);
});

test('Toolbar_WhenNoOptionsRenderProp_ShouldRenderNoExtraControl', async ({ mount, page }) => {
  await mount(<ToolbarHarness />, { hooksConfig: { route: '/p/SHIP/backlogs/stories' } });
  await expect(page.getByTestId('harness-option')).toHaveCount(0);
});
