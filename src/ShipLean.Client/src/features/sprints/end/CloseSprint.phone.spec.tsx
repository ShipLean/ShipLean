import type { Page } from 'playwright';
import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { RolloverDto, RolloverItemDto, SprintDto } from '@shiplean/core/api/sprints';
import { componentTestIds } from '../../../components/testIds';
import { sprintsTestIds as ids } from '../testIds';
import { CloseSprint } from './CloseSprint';

// Close sprint at 900px and below (SHIP-1834, SPRINT-1): item cards with the whole title and a full-width select, Set
// all to as a select above them, and a button that names the sprint.

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

const ended: SprintDto = {
  id: 's24',
  name: 'Sprint 24',
  startDate: '2026-10-12',
  endDate: '2026-10-23',
  goal: '',
  status: 'past',
  rolloverStatus: null,
  datesLabel: '12 Oct – 23 Oct',
  timeLabel: 'Ended 2 days ago',
};

const LONG_TITLE = 'Address autocomplete on the checkout page for every saved address';

const unfinished = (key: string, title: string): RolloverItemDto => ({
  workItemId: key,
  key,
  title,
  type: 'story',
  state: { id: 'st', name: 'Active', category: 'inProgress' },
  assignee: null,
  points: 3,
  openTasks: 1,
  totalTasks: 2,
  defaultTargetSprintId: 's25',
});

const rollover: RolloverDto = {
  sprint: { id: 's24', name: 'Sprint 24', status: 'past' },
  status: 'pending',
  items: [unfinished('SHIP-144', LONG_TITLE), unfinished('SHIP-129', 'Sign in with passkey')],
  nextSprint: { id: 's25', name: 'Sprint 25', status: 'current' },
  nextSprintCapacity: 25,
  nextSprintPoints: 6,
  endsEarly: false,
};

async function openDialog(mount: (c: React.JSX.Element) => Promise<unknown>, page: Page) {
  await page.route('**/api/sprints/s24/metrics', (route) =>
    route.fulfill({
      json: { sprintId: 's24', committed: 21, completed: 5, remaining: 16, burndown: [] },
    }),
  );
  await page.route('**/api/sprints/s24/rollover', (route) => route.fulfill({ json: rollover }));
  await mount(<CloseSprint project={project} sprint={ended} open onClose={() => {}} />);
  await expect(page.getByTestId(ids.endRow)).toHaveCount(2);
}

// Mutation: restoring the desktop row (truncated title beside a 220px select) fails the title and width checks.
test('CloseSprint_AtTouch_UnfinishedItemsShouldBeReadableCards', async ({ mount, page }) => {
  await openDialog(mount, page);

  const row = page.locator(`[data-testid="${ids.endRow}"][data-key="SHIP-144"]`);
  const title = row.getByTestId(ids.endRowTitle);
  await expect(title).toHaveText(LONG_TITLE);
  const clipped = await title.evaluate(
    (element) =>
      element.scrollWidth > element.clientWidth || element.scrollHeight > element.clientHeight,
  );
  expect(clipped, 'the whole title shows').toBe(false);
  await expect(row).toContainText('SHIP-144');
  await expect(row).toContainText('3 pts');
  await expect(row).toContainText('1 of 2 tasks open');
  const select = (await row.getByTestId(ids.endDestination).boundingBox())!;
  const titleBox = (await title.boundingBox())!;
  expect(select.y, 'the select sits under the title').toBeGreaterThan(titleBox.y + titleBox.height);
  expect(select.width, 'the select spans the card').toBeGreaterThan(260);
});

// Mutation: rendering the Set all to buttons at touch (dropping touch:hidden) or moving the select under the list fails.
test('CloseSprint_SetAllTo_ShouldBeASelectAtTheTop', async ({ mount, page }) => {
  await openDialog(mount, page);

  const setAll = page.getByTestId(ids.endSetAllSelect);
  await expect(setAll).toHaveValue('next');
  await expect(page.getByTestId(ids.endSetAll).first()).toBeHidden();
  const setAllBox = (await setAll.boundingBox())!;
  const firstRow = (await page.getByTestId(ids.endRow).first().boundingBox())!;
  expect(setAllBox.y + setAllBox.height).toBeLessThanOrEqual(firstRow.y);

  await setAll.selectOption('backlog');

  for (const select of await page.getByTestId(ids.endDestination).all()) {
    await expect(select).toHaveValue('backlog');
  }
  await page.getByTestId(ids.endDestination).first().selectOption('stay');
  await expect(setAll).toHaveValue('');
  await expect(page.getByTestId(ids.endApply)).toHaveText('Close Sprint 24');
  await expect(page.getByTestId(componentTestIds.dialog)).toHaveAccessibleName('Close Sprint 24');
});
