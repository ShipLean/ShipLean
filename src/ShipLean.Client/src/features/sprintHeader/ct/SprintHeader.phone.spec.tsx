import type { Page } from 'playwright';
import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '../../../api/projects';
import type { RolloverDto, SprintDto } from '../../../api/sprints';
import { componentTestIds } from '../../../components/testIds';
import { touchTestIds } from '../../../touch/testIds';
import { sprintsTestIds } from '../../sprints/testIds';
import type { SprintHeaderProps } from '../SprintHeader';
import { sprintHeaderTestIds as ids } from '../testIds';
import { sprintHeaderHarnessIds as harness } from './harnessIds';
import { SprintHeaderHarness } from './SprintHeaderHarness';

// The sprint header on a phone (SHIP-724): picker sheet, ⋯ and the full-screen edits. Runs in the CT phone project.

const THEMES = ['light', 'dark'] as const;

const project: ProjectDto = {
  id: 'p1',
  key: 'SHIP',
  name: 'Storefront',
  description: '',
  role: 'admin',
  memberCount: 3,
  workingDays: [1, 2, 3, 4, 5],
  velocityWindow: 3,
  velocityOverride: null,
  nextItemNumber: 1,
};

const sprintOf = (
  id: string,
  name: string,
  status: SprintDto['status'],
  datesLabel: string,
): SprintDto => ({
  id,
  name,
  startDate: '2026-10-19',
  endDate: '2026-10-30',
  goal: 'Customers can check out with a saved card; passkey sign-in behind a flag. Order emails go out from the new template.',
  status,
  rolloverStatus: null,
  datesLabel,
  timeLabel: '5 days left',
});

const current = sprintOf('s24', 'Sprint 24', 'current', '19 Oct – 30 Oct');
// Listed out of order on purpose: the sheet groups Past, Current, Future.
const sprints = [
  sprintOf('s25', 'Sprint 25', 'future', '2 Nov – 13 Nov'),
  current,
  sprintOf('s23', 'Sprint 23', 'past', '5 Oct – 16 Oct'),
];

const props = (over: Partial<SprintHeaderProps> = {}): SprintHeaderProps => ({
  variant: 'full',
  project,
  sprint: current,
  sprints,
  tab: 'backlog',
  ...over,
});

const actionLabels = (page: Page) =>
  page.getByTestId(touchTestIds.actionSheetRow).allTextContents();

const action = (page: Page, id: string) =>
  page.locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="${id}"]`);

const rollover: RolloverDto = {
  sprint: { id: 's24', name: 'Sprint 24', status: 'current' },
  status: 'pending',
  items: [
    {
      workItemId: 'w1',
      key: 'SHIP-141',
      title: 'Pay with saved card',
      type: 'story',
      state: { id: 'st', name: 'Active', category: 'inProgress' },
      assignee: null,
      points: 5,
      openTasks: 3,
      totalTasks: 3,
      defaultTargetSprintId: 's25',
    },
  ],
  nextSprint: { id: 's25', name: 'Sprint 25', status: 'current' },
  nextSprintCapacity: 25,
  nextSprintPoints: 29,
  endsEarly: false,
};

async function stubRollover(page: Page) {
  await page.route('**/api/sprints/s24/metrics', (route) =>
    route.fulfill({
      json: { sprintId: 's24', committed: 21, completed: 5, remaining: 16, burndown: [] },
    }),
  );
  await page.route('**/api/sprints/s24/rollover', (route) => route.fulfill({ json: rollover }));
}

test('SprintHeader_WhenMoreTappedOnPhone_ShouldOfferEditGoalEditSprintAllSprintsCloseSprint', async ({
  mount,
  page,
}) => {
  await mount(<SprintHeaderHarness {...props()} />);
  await expect(page.getByTestId(ids.allSprints)).toHaveCount(0);
  await expect(page.getByTestId(sprintsTestIds.closeSprint)).toHaveCount(0);

  await page.getByTestId(touchTestIds.moreButton).tap();

  expect(await actionLabels(page)).toEqual([
    'Edit goal',
    'Edit sprint',
    'All sprints',
    'Close sprint',
  ]);
  await action(page, 'all-sprints').tap();
  await expect(page.getByTestId(harness.location)).toHaveText('/p/SHIP/sprints');
});

test('SprintHeader_WhenSprintEndedAndClosed_ShouldLeaveCloseSprintOut', async ({ mount, page }) => {
  const closed: SprintDto = { ...current, status: 'past', rolloverStatus: 'decided' };
  await mount(<SprintHeaderHarness {...props({ sprint: closed })} />);

  await page.getByTestId(touchTestIds.moreButton).tap();

  expect(await actionLabels(page)).toEqual(['Edit goal', 'Edit sprint', 'All sprints']);
});

test('SprintHeader_WhenPickerTappedOnPhone_ShouldListPastCurrentFutureWithTheSprintTicked', async ({
  mount,
  page,
}) => {
  await mount(<SprintHeaderHarness {...props({ tab: 'capacity' })} />, {
    hooksConfig: { route: '/p/SHIP/sprints/s24/capacity' },
  });

  await page.getByTestId(ids.picker).tap();

  const rows = page.getByTestId(ids.pickerSheetRow);
  expect(await rows.evaluateAll((all) => all.map((r) => r.getAttribute('data-status')))).toEqual([
    'past',
    'current',
    'future',
  ]);
  await expect(rows.nth(1)).toContainText('19 Oct – 30 Oct');
  await expect(rows.nth(1).getByTestId(ids.pickerSheetTick)).toHaveCount(1);
  await expect(page.getByTestId(ids.pickerSheetTick)).toHaveCount(1);
  await expect(page.getByTestId(ids.pickerSheetAllSprints)).toBeVisible();
  await rows.filter({ hasText: 'Sprint 23' }).tap();
  await expect(page.getByTestId(harness.location)).toHaveText('/p/SHIP/sprints/s23/capacity');
  await expect(page.getByTestId(touchTestIds.sheet)).toHaveCount(0);
});

test('SprintHeader_WhenGoalTappedOnPhone_ShouldExpandPastTwoLines', async ({ mount, page }) => {
  await mount(<SprintHeaderHarness {...props()} />);
  const goal = page.getByTestId(ids.goal);
  const clamped = await goal.evaluate((el) => el.scrollHeight > el.clientHeight);
  expect(clamped).toBe(true);

  await page.getByTestId(ids.goalToggle).tap();

  expect(await goal.evaluate((el) => el.scrollHeight > el.clientHeight)).toBe(false);
});

test('EditSprint_WhenOpenedOnPhone_ShouldFillTheScreen', async ({ mount, page }) => {
  await mount(<SprintHeaderHarness {...props()} />);
  await page.getByTestId(touchTestIds.moreButton).tap();

  await action(page, 'edit-sprint').tap();

  const dialog = page.getByTestId(componentTestIds.dialog);
  await expect(dialog.getByTestId(sprintsTestIds.editSprint)).toBeVisible();
  expect(await dialog.boundingBox()).toEqual({ x: 0, y: 0, width: 390, height: 844 });
  await dialog.getByTestId(touchTestIds.dialogClose).tap();
  await expect(dialog).toHaveCount(0);
});

test('CloseSprint_WhenOpenedFromMore_ShouldFillTheScreenWithButtonAtBottom', async ({
  mount,
  page,
}) => {
  await stubRollover(page);
  await mount(<SprintHeaderHarness {...props()} />);
  await page.getByTestId(touchTestIds.moreButton).tap();

  await action(page, 'close-sprint').tap();

  const dialog = page.getByTestId(componentTestIds.dialog);
  await expect(dialog.getByTestId(sprintsTestIds.endRow)).toHaveCount(1);
  expect(await dialog.boundingBox()).toEqual({ x: 0, y: 0, width: 390, height: 844 });
  const apply = await dialog.getByTestId(sprintsTestIds.endApply).boundingBox();
  expect(apply).not.toBeNull();
  // The button spans the foot of the screen: within the bottom 64px and most of the width.
  expect(apply!.y + apply!.height).toBeGreaterThan(844 - 64);
  expect(apply!.width).toBeGreaterThan(300);
  await expect(dialog.getByTestId(sprintsTestIds.endCancel)).toBeHidden();
  await dialog.getByTestId(touchTestIds.dialogClose).tap();
  await expect(dialog).toHaveCount(0);
});

for (const theme of THEMES) {
  test(`SprintHeader_WhenShownOnPhone_ShouldMatchItsScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mount(<SprintHeaderHarness {...props()} />);
    await expect(page.getByTestId(ids.header)).toHaveScreenshot(
      `sprint-header-phone.${theme}.png`,
      { animations: 'disabled' },
    );
  });

  test(`SprintHeader_WhenSlimOnPhone_ShouldMatchItsScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await mount(<SprintHeaderHarness {...props({ variant: 'slim', tab: 'taskboard' })} />);
    await expect(page.getByTestId(ids.goal)).toBeVisible();
    await expect(page.getByTestId(ids.tabs)).toBeVisible();
    await expect(page.getByTestId(ids.header)).toHaveScreenshot(
      `sprint-header-phone-slim.${theme}.png`,
      { animations: 'disabled' },
    );
  });
}
