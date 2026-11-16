import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { BacklogViewDto } from '@shiplean/core/api/views';
import { componentTestIds } from '../../../components/testIds';
import { touchTestIds } from '../../../touch/testIds';
import { backlogView, itemRow, planningView, PROJECT, review } from '../ct/fixtures';
import { ListAddressHarness } from '../ct/ListAddressHarness';
import { RowActionsHarness } from '../ct/RowActionsHarness';
import { rowActionsHarnessIds } from '../ct/rowActionsHarnessIds';
import { stubBacklog } from '../ct/stubApi';
import { ListMode } from '../list/ListMode';
import { PlanningMode } from '../planning/PlanningMode';
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

const scale = (page: Page, points: number) =>
  page.getByTestId(ids.pointsScale).and(page.locator(`[data-points="${points}"]`));
const openQueue = async (page: Page) => {
  await page.getByTestId(ids.notEstimatedEstimate).tap();
  await expect(page.getByTestId(ids.pointsSheet)).toBeVisible();
};

test('PointsSheet_WhenAScaleButtonIsTapped_ShouldSaveAndStepToTheNext', async ({ mount, page }) => {
  const server = await stubBacklog(page, queueView([ROWS[1].id, ROWS[2].id]));
  await mount(<ListMode project={PROJECT} />, route);
  await openQueue(page);
  await expect(page.getByTestId(ids.queueStepKey)).toHaveText('SHIP-2');

  await scale(page, 5).tap();

  await expect(page.getByTestId(ids.queueStepKey)).toHaveText('SHIP-3');
  await expect(page.getByTestId(ids.queuePosition)).toContainText('2 of 2');
  expect(server.patches).toHaveLength(1);
  expect(server.patches[0].url).toContain('/api/work-items/id-SHIP-2');
  expect(JSON.stringify(server.patches[0].body)).toContain('"points":{"value":5}');
});

test('PointsSheet_WhenSaveFails_ShouldStayOnTheItemAndSaySo', async ({ mount, page }) => {
  const server = await stubBacklog(page, queueView([ROWS[1].id, ROWS[2].id]), {
    patchAnswers: [{ status: 500, body: { error: 'The database is down', code: 'internal' } }],
  });
  await mount(<ListMode project={PROJECT} />, route);
  await openQueue(page);

  await scale(page, 3).tap();

  await expect(page.getByTestId(ids.pointsSheetError)).toContainText("SHIP-2 wasn't saved");
  await expect(page.getByTestId(ids.queueStepKey)).toHaveText('SHIP-2');
  await expect(page.getByTestId(ids.queuePosition)).toContainText('1 of 2');
  expect(server.patches).toHaveLength(1);
});

const conflictOn = (key: string) => ({
  status: 409,
  body: {
    error: 'This item changed.',
    code: 'conflict',
    current: { ...ROWS[1], key, points: 8, revision: 9 },
    theirChanges: [{ rev: 9, changedBy: { name: 'Tom Okafor' }, changes: [] }],
  },
});

test('PointsSheet_WhenUseMineAfterAConflict_ShouldSaveAndStepToTheNext', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, queueView([ROWS[1].id, ROWS[2].id]), {
    patchAnswers: [
      conflictOn('SHIP-2'),
      {
        status: 200,
        body: { id: 'id-SHIP-2', key: 'SHIP-2', revision: 10, changes: [], notices: [] },
      },
    ],
  });
  await mount(<ListMode project={PROJECT} />, route);
  await openQueue(page);
  await scale(page, 3).tap();
  await expect(page.getByTestId(componentTestIds.conflictBanner)).toContainText('Tom Okafor');

  await page.getByTestId(componentTestIds.conflictUseMine).tap();

  await expect(page.getByTestId(ids.queueStepKey)).toHaveText('SHIP-3');
  await expect(page.getByTestId(ids.queuePosition)).toContainText('2 of 2');
  await expect(page.getByTestId(ids.pointsSheetError)).toHaveCount(0);
  expect(server.patches).toHaveLength(2);
  expect(JSON.stringify(server.patches[1].body)).toContain('"expectedRevision":9');
});

test('PointsSheet_WhenKeepTheirsAfterAConflict_ShouldStayOnTheItemWithNoError', async ({
  mount,
  page,
}) => {
  const server = await stubBacklog(page, queueView([ROWS[1].id, ROWS[2].id]), {
    patchAnswers: [conflictOn('SHIP-2')],
  });
  await mount(<ListMode project={PROJECT} />, route);
  await openQueue(page);
  await scale(page, 3).tap();
  await expect(page.getByTestId(componentTestIds.conflictBanner)).toBeVisible();

  await page.getByTestId(componentTestIds.conflictKeepTheirs).tap();

  await expect(page.getByTestId(componentTestIds.conflictBanner)).toHaveCount(0);
  await expect(scale(page, 3)).toBeEnabled();
  await expect(page.getByTestId(ids.pointsSheetError)).toHaveCount(0);
  await expect(page.getByTestId(ids.queueStepKey)).toHaveText('SHIP-2');
  expect(server.patches).toHaveLength(1);
});

test('PointsSheet_WhileASaveIsInFlight_ShouldDisableSkip', async ({ mount, page }) => {
  let release: () => void = () => undefined;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  await stubBacklog(page, queueView([ROWS[1].id, ROWS[2].id]), { holdFirstPatch: held });
  await mount(<ListMode project={PROJECT} />, route);
  await openQueue(page);

  await scale(page, 5).tap();

  await expect(page.getByTestId(ids.queueSkip)).toBeDisabled();
  release();
  await expect(page.getByTestId(ids.queueStepKey)).toHaveText('SHIP-3');
  await expect(page.getByTestId(ids.queueSkip)).toBeEnabled();
});

test('PointsSheet_FromTheQueue_ShouldShowPositionAndSkip', async ({ mount, page }) => {
  const server = await stubBacklog(page, queueView([ROWS[1].id, ROWS[2].id]));
  await mount(<ListMode project={PROJECT} />, route);
  await openQueue(page);
  await expect(page.getByTestId(touchTestIds.sheetTitle)).toHaveText('Estimate');
  await expect(page.getByTestId(ids.queuePosition)).toContainText('1 of 2');
  await expect(page.getByTestId(ids.queueStepKey)).toHaveText('SHIP-2');

  await page.getByTestId(ids.queueSkip).tap();
  await expect(page.getByTestId(ids.queueStepKey)).toHaveText('SHIP-3');
  await expect(page.getByTestId(ids.queuePosition)).toContainText('2 of 2');

  await page.getByTestId(ids.queueSkip).tap();
  await expect(page.getByTestId(ids.queueEmpty)).toBeVisible();
  expect(server.patches).toHaveLength(0);
});

test('PointsSheet_FromTheQueue_WhenScrimTapped_ShouldLeaveTheQueueWithoutOpeningARow', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, queueView([ROWS[1].id, ROWS[2].id]));
  await mount(<ListAddressHarness project={PROJECT} />, route);
  await openQueue(page);
  // The list behind keeps every row at touch (SHIP-1991), and SHIP-2's row near the top sits under the scrim.
  const row = await rowOf(page, 'SHIP-2').boundingBox();

  await page
    .getByTestId(touchTestIds.sheetScrim)
    .tap({ position: { x: row!.x + 40, y: row!.y + row!.height / 2 } });

  await expect(page.getByTestId(ids.pointsSheet)).toHaveCount(0);
  await expect(page.getByTestId(ids.row)).toHaveCount(4);
  await expect(page.getByTestId(rowActionsHarnessIds.address)).toHaveText(
    '/p/SHIP/backlogs/stories',
  );
});

test('EstimateSheet_OnTheList_ShouldKeepEveryRowBehindItAndReorderingOn', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, queueView([ROWS[1].id, ROWS[2].id]));
  await mount(<ListMode project={PROJECT} />, route);
  await openQueue(page);

  await expect(page.getByTestId(ids.row)).toHaveCount(4);
  await expect(rowOf(page, 'SHIP-1')).toHaveCount(1);
  await expect(page.getByTestId(ids.dragOffBanner)).toHaveCount(0);
});

test('EstimateSheet_OnPlanning_ShouldKeepEveryRowBehindItAndDraggingOn', async ({
  mount,
  page,
}) => {
  await stubBacklog(page, { ...planningView(), review: review({ unestimated: ['id-SHIP-171'] }) });
  await mount(<PlanningMode project={PROJECT} />, {
    hooksConfig: { route: '/p/SHIP/backlogs/stories?mode=planning' },
  });
  await expect(rowOf(page, 'SHIP-170')).toBeVisible();
  await openQueue(page);

  await expect(page.getByTestId(ids.queueStepKey)).toHaveText('SHIP-171');
  await expect(rowOf(page, 'SHIP-170')).toHaveCount(1);
  await expect(rowOf(page, 'SHIP-160')).toHaveCount(1);
  await expect(page.getByTestId(ids.dragOffBanner)).toHaveCount(0);
});

test('NotEstimatedBanner_AtTouch_ShouldShowOnlyTheUnestimatedCheck', async ({ mount, page }) => {
  await stubBacklog(page, {
    ...backlogView(ROWS),
    review: review({
      unestimated: [ROWS[1].id, ROWS[2].id],
      no_parent: [ROWS[0].id],
      stale: [ROWS[3].id],
    }),
  });
  await mount(<ListMode project={PROJECT} />, route);

  await expect(page.getByTestId(ids.notEstimatedBanner)).toHaveText(
    '2 not estimated in the top 100.Estimate',
  );
  await expect(page.getByTestId(ids.planStrip)).toHaveCount(0);
  await expect(page.getByTestId(ids.planCheck)).toHaveCount(0);
});

test('NotEstimatedBanner_WhenEverythingIsEstimated_ShouldNotShow', async ({ mount, page }) => {
  await stubBacklog(page, {
    ...backlogView(ROWS),
    review: review({ no_parent: [ROWS[0].id] }),
  });
  await mount(<ListMode project={PROJECT} />, route);

  await expect(page.getByTestId(ids.row)).toHaveCount(4);
  await expect(page.getByTestId(ids.notEstimatedBanner)).toHaveCount(0);
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

test('RowMore_ShouldShowTheCurrentSprintAndParentAsValues', async ({ mount, page }) => {
  const placed = itemRow({
    key: 'SHIP-7',
    rank: 5,
    revision: 5,
    points: 8,
    sprint: { id: 'sp25', name: 'Sprint 25', status: 'future' },
    parent: { id: 'f1', key: 'SHIP-90', type: { id: 'feature', name: 'Feature' }, title: 'Checkout v2' },
  });
  await stubBacklog(page, backlogView([...ROWS, placed]), { sprints: SPRINTS });
  await mount(<RowActionsHarness project={PROJECT} />, route);

  const detail = (action: string) =>
    page
      .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="${action}"]`)
      .getByTestId(touchTestIds.actionSheetDetail);
  await openMore(page, 'SHIP-7');
  await expect(page.getByTestId(touchTestIds.sheet)).toContainText('Title of SHIP-7');
  await expect(detail('sprint')).toHaveText('Sprint 25');
  await expect(detail('parent')).toHaveText('Checkout v2');
  await expect(detail('points')).toHaveText('8');
  await page.getByTestId(touchTestIds.sheetClose).tap();

  // A row in no sprint and with no parent says so.
  await openMore(page, 'SHIP-1');
  await expect(detail('sprint')).toHaveText('Backlog');
  await expect(detail('parent')).toHaveText('None');
});
