import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { BacklogViewDto } from '@shiplean/core/api/views';
import { touchTestIds } from '../../../touch/testIds';
import { backlogView, itemRow, planningView, PROJECT, review } from '../ct/fixtures';
import { RowActionsHarness } from '../ct/RowActionsHarness';
import { rowActionsHarnessIds } from '../ct/rowActionsHarnessIds';
import { stubBacklog } from '../ct/stubApi';
import { ListMode } from '../list/ListMode';
import { backlogTestIds as ids } from '../testIds';

// The row ⋯, the Points sheet and the not-estimated queue on a phone (SHIP-716 D9). Runs in the CT phone project.

const route = { hooksConfig: { route: '/p/SHIP/backlogs/stories' } };
const ROWS = [1, 2, 3, 4].map((n) =>
  itemRow({ key: `SHIP-${n}`, rank: n, revision: n, points: n === 2 || n === 3 ? null : 5 }),
);
const SPRINTS = [
  {
    id: 'sp25',
    name: 'Sprint 25',
    startDate: '2026-11-03',
    endDate: '2026-11-16',
    goal: '',
    status: 'future' as const,
    rolloverStatus: null,
    datesLabel: '3 Nov – 16 Nov',
    timeLabel: '',
  },
];
const rowOf = (page: Page, key: string) =>
  page.getByTestId(ids.row).and(page.locator(`[data-key="${key}"]`));
const openMore = async (page: Page, key: string) => {
  await rowOf(page, key).getByTestId(touchTestIds.moreButton).tap();
  await expect(page.getByTestId(touchTestIds.sheet)).toBeVisible();
};
const pick = (page: Page, action: string) =>
  page.locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="${action}"]`).tap();

test('RowActionsSheet_WhenSprintPicked_ShouldMoveToSprint', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS), { sprints: SPRINTS });
  await mount(<RowActionsHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(4);

  await openMore(page, 'SHIP-3');
  await pick(page, 'sprint');
  const option = page.locator(`[data-testid="${ids.rowSprintOption}"][data-sprint="sp25"]`);
  await expect(option).toContainText('Sprint 25');
  await option.tap();

  await expect.poll(() => server.bulks.length).toBe(1);
  expect(server.bulks[0].items).toHaveLength(1);
  expect(server.bulks[0].items[0].id).toBe('id-SHIP-3');
  expect(server.bulks[0].items[0].changes?.sprint).toEqual({ value: 'sp25' });
  expect(server.bulks[0].items[0].placement).toBeNull();
});

test('RowActionsSheet_WhenSelectTapped_ShouldStartSelectMode', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<RowActionsHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(4);

  await openMore(page, 'SHIP-2');
  await pick(page, 'select');

  const row = rowOf(page, 'SHIP-2');
  await expect(row.getByTestId(ids.touchRowSelect)).toBeChecked();
  await expect(row).toHaveAttribute('data-selected', 'true');
  await expect(page.getByTestId(ids.touchRowSelect)).toHaveCount(4);
  await expect(page.getByTestId(touchTestIds.moreButton)).toHaveCount(0);
});

test('RowActionsSheet_WhenScrimTapped_ShouldNotOpenItem', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<RowActionsHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(4);

  await openMore(page, 'SHIP-1');
  // The scrim covers the row, so a tap at the row's place lands on it and closes the sheet.
  await page.getByTestId(touchTestIds.sheetScrim).tap({ position: { x: 20, y: 20 } });

  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  await expect(page.getByTestId(rowActionsHarnessIds.address)).toHaveText(
    '/p/SHIP/backlogs/stories',
  );
});

test('PointsChip_WhenOutsideSelectionProvider_ShouldNotOpenSheet', async ({ mount, page }) => {
  await stubBacklog(page, backlogView(ROWS));
  await mount(<RowActionsHarness project={PROJECT} tree />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(4);

  const chip = rowOf(page, 'SHIP-2').getByTestId(ids.points);
  await expect(chip).toHaveText('?');
  expect(await chip.evaluate((element) => element.tagName)).toBe('SPAN');
  await chip.tap();

  await expect(page.getByTestId(ids.pointsSheet)).toHaveCount(0);
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
  // Tree rows still get the ⋯, without Points or Select.
  await openMore(page, 'SHIP-1');
  await expect(page.locator('[data-action-id="points"]')).toHaveCount(0);
  await expect(page.locator('[data-action-id="select"]')).toHaveCount(0);
});

const queueView = (flagged: string[]): BacklogViewDto => ({
  ...backlogView(ROWS),
  review: review({ unestimated: flagged }),
});

test('BeforeYouPlan_WhenTouchQueueSkipped_ShouldStepToNext', async ({ mount, page }) => {
  await stubBacklog(page, queueView([ROWS[1].id, ROWS[2].id]));
  await mount(<ListMode project={PROJECT} />, route);
  await page.getByTestId(ids.planCheck).and(page.locator('[data-check="unestimated"]')).tap();

  await expect(page.getByTestId(ids.queueBanner)).toBeVisible();
  await expect(page.getByTestId(ids.queueStepKey)).toHaveText('SHIP-2');
  await expect(page.getByTestId(ids.queueBanner)).toContainText('1 of 2');

  await page.getByTestId(ids.queueSkip).tap();

  await expect(page.getByTestId(ids.queueStepKey)).toHaveText('SHIP-3');
  await expect(page.getByTestId(ids.queueBanner)).toContainText('2 of 2');
});

test('PointsSheet_WhenSaved_ShouldPatchPoints', async ({ mount, page }) => {
  const server = await stubBacklog(page, backlogView(ROWS));
  await mount(<RowActionsHarness project={PROJECT} />, route);
  await expect(page.getByTestId(ids.row)).toHaveCount(4);

  await rowOf(page, 'SHIP-2').getByTestId(ids.points).tap();
  await page.getByTestId(ids.pointsSheetInput).fill('8');
  await page.getByTestId(ids.pointsSheetSave).tap();

  await expect(page.getByTestId(ids.pointsSheet)).toHaveCount(0);
  await expect.poll(() => server.patches.length).toBe(1);
  expect(server.patches[0].url).toContain('/api/work-items/id-SHIP-2');
  expect(JSON.stringify(server.patches[0].body)).toContain('"points":{"value":8}');
});

// What switching List and Planning leaves in the cache: a read of the mode not shown, with the same revision of the row.
const SPRINT_ROW = itemRow({
  key: 'SHIP-160',
  rank: 1,
  sprint: { id: 'sp24', name: 'Sprint 24', status: 'current' },
});
const listWithSprintRow = () => backlogView([SPRINT_ROW]);
const edgeBody = async (page: Page, server: { bulks: { items: unknown[] }[] }) => {
  await openMore(page, 'SHIP-160');
  await pick(page, 'top');
  await expect.poll(() => server.bulks.length).toBe(1);
  return server.bulks[0].items[0] as {
    id: string;
    changes: { sprint: { value: string | null } } | null;
    placement: { slot: string };
  };
};

test('RowActionsSheet_WhenMovedToTopOnPlanningWithListCached_ShouldLeaveItsSprint', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, planningView());
  await mount(
    <RowActionsHarness project={PROJECT} mode="planning" leftCached={listWithSprintRow()} />,
    route,
  );
  await expect(rowOf(page, 'SHIP-160')).toBeVisible();

  const item = await edgeBody(page, server);

  expect(item.id).toBe('id-SHIP-160');
  expect(item.placement.slot).toBe('first');
  expect(item.changes?.sprint).toEqual({ value: null });
});

test('RowActionsSheet_WhenMovedToTopOnListWithPlanningCached_ShouldKeepItsSprint', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, listWithSprintRow());
  await mount(<RowActionsHarness project={PROJECT} leftCached={planningView()} />, route);
  await expect(rowOf(page, 'SHIP-160')).toBeVisible();

  const item = await edgeBody(page, server);

  expect(item.placement.slot).toBe('first');
  expect(item.changes).toBeNull();
});
