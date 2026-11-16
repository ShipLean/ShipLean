import type { Page } from 'playwright';
import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintCapacityDto, SprintDto } from '@shiplean/core/api/sprints';
import { touchTestIds } from '../../../../touch/testIds';
import { sprintsTestIds as ids } from '../../testIds';
import { NewSprintRail } from '../../newSprint/NewSprintRail';
import { SprintsList } from '../SprintsList';
import { SprintsTouchHead } from '../SprintsTouchHead';

// All sprints on a phone (SHIP-723, L723a): rows with a ⋯ sheet, capacity notes as text and the full-width velocity chart.
// Runs in the CT phone project. ND1 (SHIP-1846): sections, labelled figures, the K1 ⋯ sheet, the empty panel and the head.

const THEMES = ['light', 'dark'] as const;

const project: ProjectDto = {
  id: 'p1',
  key: 'SHIP',
  name: 'Storefront',
  description: '',
  role: 'member',
  memberCount: 3,
  workingDays: [1, 2, 3, 4, 5],
  velocityWindow: 3,
  velocityOverride: null,
  nextItemNumber: 1,
};

const sprintOf = (n: number, status: SprintDto['status'], goal = ''): SprintDto => ({
  id: `s${n}`,
  name: `Sprint ${n}`,
  startDate: '2026-10-05',
  endDate: '2026-10-16',
  goal,
  status,
  rolloverStatus: null,
  datesLabel: `${n} Oct – ${n + 11} Oct`,
  timeLabel: '',
});

const sprints = [
  sprintOf(
    23,
    'past',
    'Ship the saved card picker and the passkey flag, then tidy the order emails.',
  ),
  sprintOf(24, 'current', 'Check out with a saved card'),
  sprintOf(25, 'future'),
];

const member = (name: string, points: number, note: string | null) => ({
  user: { id: name, name, initials: name[0]!, avatarColour: 'blue', photoUrl: null },
  days: null,
  points,
  note,
});

const capacityOf = (sprintId: string): SprintCapacityDto => ({
  sprintId,
  members:
    sprintId === 's24' ? [member('Priya', 8, 'Away Friday'), member('Marcus', 13, null)] : [],
  velocity: null,
  velocityDefault: 0,
  workingDays: 10,
  defaultDays: 10,
  holidays: [],
  holidayCountry: null,
  ended: false,
  teamTotal: sprintId === 's24' ? 21 : 0,
});

async function stub(page: Page) {
  await page.route('**/api/projects/p1/velocity', (route) =>
    route.fulfill({
      json: {
        window: 3,
        average: 19,
        override: null,
        sprints: [
          { sprintId: 's22', sprintName: 'Sprint 22', committed: 18, completed: 16 },
          { sprintId: 's23', sprintName: 'Sprint 23', committed: 20, completed: 21 },
        ],
      },
    }),
  );
  await page.route('**/api/sprints/*/metrics', (route) => {
    const id = route.request().url().split('/').at(-2)!;
    return route.fulfill({
      json: { sprintId: id, committed: 20, completed: 9, remaining: 0, burndown: [] },
    });
  });
  await page.route('**/api/sprints/*/capacity', (route) => {
    const id = route.request().url().split('/').at(-2)!;
    return route.fulfill({ json: capacityOf(id) });
  });
}

const rowOf = (page: Page, name: string) =>
  page
    .getByTestId(ids.listRow)
    .filter({ has: page.getByTestId(ids.rowLink).filter({ hasText: new RegExp(`^${name}$`) }) });

const sheetActionIds = (page: Page) =>
  page
    .getByTestId(touchTestIds.actionSheetRow)
    .evaluateAll((rows) => rows.map((row) => row.getAttribute('data-action-id')));

// ND1-5 (SHIP-1807 P6). Mutation: dropping the status check on Close sprint put close-sprint on Sprint 23's sheet.
test('SprintsList_WhenRowMoreTapped_ShouldOfferTheSprintPagesThenEditAndCloseSprint', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SprintsList project={project} sprints={sprints} />);

  await rowOf(page, 'Sprint 24').getByTestId(touchTestIds.moreButton).tap();

  await expect(page.getByTestId(touchTestIds.actionSheetRow).first()).toBeVisible();
  expect(await sheetActionIds(page)).toEqual([
    'backlog',
    'taskboard',
    'capacity',
    'retro',
    'demo',
    'edit-sprint',
    'close-sprint',
  ]);
  await expect(page.getByTestId(touchTestIds.actionSheetGroup)).toContainText(['Open']);
  await expect(page.getByTestId(touchTestIds.actionSheetFooter)).toHaveCount(0);
  await page
    .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="edit-sprint"]`)
    .tap();
  await expect(page.getByTestId(ids.editSprint)).toBeVisible();
});

test('SprintsList_WhenFutureOrPastRowMoreTapped_ShouldLeaveOutWhatDoesNotApply', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SprintsList project={project} sprints={sprints} />);

  await rowOf(page, 'Sprint 25').getByTestId(touchTestIds.moreButton).tap();
  await expect(page.getByTestId(touchTestIds.actionSheetRow).first()).toBeVisible();
  expect(await sheetActionIds(page)).toEqual(['backlog', 'taskboard', 'capacity', 'edit-sprint']);
  await page.getByTestId(touchTestIds.sheetClose).tap();

  await rowOf(page, 'Sprint 23').getByTestId(touchTestIds.moreButton).tap();
  await expect(page.getByTestId(touchTestIds.actionSheetRow).first()).toBeVisible();
  expect(await sheetActionIds(page)).toEqual([
    'backlog',
    'taskboard',
    'capacity',
    'retro',
    'demo',
    'edit-sprint',
  ]);
  await expect(page.getByTestId(touchTestIds.actionSheetFooter)).toContainText('Ended.');
});

// ND1-2 (P2). Mutation: rendering every sprint in one section put all six rows under Current.
test('SprintsList_WhenOpenedOnPhone_ShouldGroupSprintsUnderCurrentFuturePastHeadings', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SprintsList project={project} sprints={sprints} />);

  const sections = page.getByTestId(ids.listSection);
  await expect(sections.locator('h2')).toHaveText(['Current', 'Future · 1', 'Past · 1']);
  await expect(sections.nth(1).getByTestId(ids.listRow)).toHaveAttribute(
    'data-sprint',
    'Sprint 25',
  );
  await expect(page.getByTestId(ids.rowStatus)).toHaveCount(0);
});

// ND1-3 (P4). Mutation: removing the Completed label left "Committed 20 9 so far".
test('SprintsList_WhenRowShownOnPhone_ShouldLabelCapacityCommittedAndCompleted', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SprintsList project={project} sprints={sprints} />);

  await expect(rowOf(page, 'Sprint 24')).toContainText('Capacity 21Committed 20Completed 9 so far');
  await expect(rowOf(page, 'Sprint 25')).toContainText(
    'Capacity 19 · velocityCommitted 20Completed —',
  );
});

// ND1-4 (P4). Mutation: removing after:inset-0 from the name link made the goal's centre hit the li, not the link.
test('SprintsList_WhenRowTappedAwayFromTheName_ShouldHitTheBacklogLink', async ({
  mount,
  page,
}) => {
  await stub(page);
  await mount(<SprintsList project={project} sprints={sprints} />);

  const goal = rowOf(page, 'Sprint 23').getByTestId(ids.rowGoal);
  await expect(goal).toBeVisible();
  const box = (await goal.boundingBox())!;
  const hit = await page.evaluate(
    ([x, y]) => document.elementFromPoint(x!, y!)?.closest('a')?.getAttribute('data-testid'),
    [box.x + box.width / 2, box.y + box.height / 2],
  );
  expect(hit).toBe(ids.rowLink);
});

// ND1-6 (P7). Mutation: dropping the empty early return showed the velocity card over an empty list.
test('SprintsList_WhenNoSprintsOnPhone_ShouldShowOnlyTheEmptyPanel', async ({ mount, page }) => {
  await stub(page);
  await page.route('**/api/projects/p1/sprints/next', (route) =>
    route.fulfill({ json: { name: 'Sprint 1', startDate: '', endDate: '', goal: '' } }),
  );
  await mount(<SprintsList project={project} sprints={[]} />);

  await expect(page.getByTestId(ids.listEmpty)).toContainText('No sprints yet');
  await expect(page.getByTestId(ids.velocity)).toHaveCount(0);
  await expect(page.getByTestId(ids.listTouch)).toHaveCount(0);
  await page.getByTestId(ids.listEmptyNew).tap();
  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('Sprint 1');
});

// ND1-1 (P1). Mutation: dropping the planning mode from the link sent Plan sprints to the plain Stories list.
test('SprintsTouchHead_WhenSprintsExist_ShouldShowTheCountAndPlanSprints', async ({
  mount,
  page,
}) => {
  await mount(<SprintsTouchHead project={project} count={8} />);

  await expect(page.getByTestId(ids.listHead)).toContainText('Storefront · 8 sprints');
  await expect(page.getByTestId(ids.planSprints)).toHaveAttribute(
    'href',
    '/p/SHIP/backlogs/stories?mode=planning',
  );
});

test('SprintsList_WhenCapacityHasNotes_ShouldShowThemWithoutHover', async ({ mount, page }) => {
  await stub(page);
  await mount(<SprintsList project={project} sprints={sprints} />);

  const row = rowOf(page, 'Sprint 24');
  await expect(row.getByTestId(ids.rowNotes)).toHaveText('Priya: Away Friday');
  await expect(rowOf(page, 'Sprint 23').getByTestId(ids.rowNotes)).toHaveCount(0);
  await expect(page.getByTestId(ids.list)).toHaveCount(0);
});

test('SprintsList_WhenVelocityDrawn_ShouldShowEveryBarsValueAsText', async ({ mount, page }) => {
  await stub(page);
  await mount(<SprintsList project={project} sprints={sprints} />);

  // Two sprints ended: the older one's bars are valued by velocityValue, the latest's by velocityLatest.
  await expect(page.getByTestId(ids.velocityValue)).toHaveText(['18', '16']);
  await expect(page.getByTestId(ids.velocityLatest)).toHaveText(['20', '21']);
});

test('SprintsList_WhenEditSprintClosed_ShouldReturnFocusToTheRowMore', async ({ mount, page }) => {
  await stub(page);
  await mount(<SprintsList project={project} sprints={sprints} />);

  const more = rowOf(page, 'Sprint 24').getByTestId(touchTestIds.moreButton);
  await more.tap();
  await page
    .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="edit-sprint"]`)
    .tap();
  await expect(page.getByTestId(ids.editSprint)).toBeVisible();
  await page.getByTestId(touchTestIds.dialogClose).tap();

  await expect(page.getByTestId(ids.editSprint)).toHaveCount(0);
  await expect(more).toBeFocused();
});

test('NewSprintRail_WhenOpenedOnPhone_ShouldHaveCreateInTheHeaderAndNoFormSubmit', async ({
  mount,
  page,
}) => {
  await page.route('**/api/projects/p1/sprints/next', (route) =>
    route.fulfill({
      json: { name: 'Sprint 27', startDate: '2026-11-30', endDate: '2026-12-11', goal: '' },
    }),
  );
  await mount(<NewSprintRail projectId="p1" onClose={() => {}} />);

  await expect(page.getByTestId(ids.newSprintName)).toHaveValue('Sprint 27');
  await expect(
    page.getByTestId(touchTestIds.dialogPrimary).getByTestId(ids.newSprintCreate),
  ).toBeVisible();
  await expect(page.getByTestId(ids.newSprintSubmit)).toHaveCount(0);
  // The form ends with its error line: no empty button row under it.
  await expect(page.locator('form > :last-child')).toHaveAttribute(
    'data-testid',
    ids.newSprintError,
  );
});

for (const theme of THEMES) {
  test(`SprintsListPhone_WhenOpened_ShouldMatchItsScreenshot_sprints-list-phone_${theme}`, async ({
    mount,
    page,
  }) => {
    expect(page.viewportSize()?.width, 'phone shots run only in the CT phone project').toBe(390);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await stub(page);
    const screen = await mount(<SprintsList project={project} sprints={sprints} />);
    await expect(page.getByTestId(ids.velocityBar).first()).toBeVisible();
    await expect(page.getByTestId(ids.rowCommitted).first()).not.toHaveText('');
    await expect(rowOf(page, 'Sprint 24').getByTestId(ids.rowNotes)).toBeVisible();
    await expect(screen).toHaveScreenshot(`sprints-list-phone.${theme}.png`, {
      animations: 'disabled',
    });
  });
}
