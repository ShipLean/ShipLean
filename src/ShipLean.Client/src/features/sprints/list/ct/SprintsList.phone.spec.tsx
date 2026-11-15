import type { Page } from 'playwright';
import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintCapacityDto, SprintDto } from '@shiplean/core/api/sprints';
import { touchTestIds } from '../../../../touch/testIds';
import { sprintsTestIds as ids } from '../../testIds';
import { NewSprintRail } from '../../newSprint/NewSprintRail';
import { SprintsList } from '../SprintsList';

// All sprints on a phone (SHIP-723, L723a): rows with a ⋯ sheet, capacity notes as text and the full-width velocity chart.
// Runs in the CT phone project.

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

test('SprintsList_WhenRowMoreTapped_ShouldOfferOpenAndEditSprint', async ({ mount, page }) => {
  await stub(page);
  await mount(<SprintsList project={project} sprints={sprints} />);

  await rowOf(page, 'Sprint 24').getByTestId(touchTestIds.moreButton).tap();

  const rows = page.getByTestId(touchTestIds.actionSheetRow);
  await expect(rows).toHaveText(['Open', 'Edit sprint']);
  await page
    .locator(`[data-testid="${touchTestIds.actionSheetRow}"][data-action-id="edit-sprint"]`)
    .tap();
  await expect(page.getByTestId(ids.editSprint)).toBeVisible();
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
