import type { Page } from 'playwright';
import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { RolloverDto, RolloverItemDto, SprintDto } from '@shiplean/core/api/sprints';
import { componentTestIds } from '../../../components/testIds';
import { ToastHost } from '../../../components/ToastHost';
import { sprintsTestIds as ids } from '../testIds';
import { CloseSprint } from './CloseSprint';

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

const sprintRef = (id: string, name: string, status: 'past' | 'current') => ({ id, name, status });

const unfinished = (key: string, points: number, open: number, total: number): RolloverItemDto => ({
  workItemId: key,
  key,
  title: `Title of ${key}`,
  type: 'story',
  state: { id: 'st', name: 'Active', category: 'inProgress' },
  assignee: null,
  points,
  openTasks: open,
  totalTasks: total,
  defaultTargetSprintId: 's25',
});

const rollover = (overrides: Partial<RolloverDto> = {}): RolloverDto => ({
  sprint: sprintRef('s24', 'Sprint 24', 'past'),
  status: 'pending',
  items: [unfinished('SHIP-129', 5, 2, 3), unfinished('SHIP-144', 3, 1, 1)],
  nextSprint: sprintRef('s25', 'Sprint 25', 'current'),
  nextSprintCapacity: 12,
  nextSprintPoints: 6,
  endsEarly: false,
  ...overrides,
});

interface Posted {
  moves: { workItemId: string; targetSprintId: string | null }[];
  endToday?: boolean;
}

async function stub(page: Page, current: () => RolloverDto, posts: Posted[] = []) {
  await page.route('**/api/sprints/s24/metrics', (route) =>
    route.fulfill({
      json: { sprintId: 's24', committed: 21, completed: 10, remaining: 11, burndown: [] },
    }),
  );
  await page.route('**/api/sprints/s24/rollover', (route) => {
    if (route.request().method() === 'POST') {
      posts.push(route.request().postDataJSON() as Posted);
      return route.fulfill({ json: { ...ended, rolloverStatus: 'decided', status: 'past' } });
    }
    return route.fulfill({ json: current() });
  });
}

const mountButton = (mount: (c: React.JSX.Element) => Promise<unknown>, sprint = ended) =>
  mount(
    <>
      <CloseSprint project={project} sprint={sprint} />
      <ToastHost />
    </>,
  );

// Mounts the button and presses it.
const openDialog = async (mount: (c: React.JSX.Element) => Promise<unknown>, page: Page) => {
  await mountButton(mount);
  await page.getByTestId(ids.closeSprint).click();
};

test('CloseSprint_WhenSprintEndedWithUnfinishedItems_ShouldListThemDefaultingToTheNextSprint', async ({
  mount,
  page,
}) => {
  await stub(page, () => rollover());
  await openDialog(mount, page);

  const rows = page.getByTestId(ids.endRow);
  await expect(rows).toHaveCount(2);
  await expect(page.getByTestId(componentTestIds.dialog)).toContainText(
    '10 of 21 committed points completed · 2 items unfinished (8 pts)',
  );
  await expect(page.getByTestId(ids.endHistory)).toContainText(
    'Sprint 24 keeps 21 committed / 10 completed for velocity',
  );
  await expect(page.getByTestId(ids.endHistory)).toContainText(
    'Committed is what was in it at 00:00 UTC on its start date',
  );
  await expect(rows.first()).toContainText('SHIP-129');
  await expect(rows.first()).toContainText('5 pts');
  await expect(rows.first()).toContainText('2 of 3 tasks open');
  await expect(rows.first().getByTestId(ids.endDestination)).toHaveValue('next');
  await expect(rows.first().getByTestId(ids.endDestination)).toContainText('Move to Sprint 25');
  await expect(page.getByTestId(ids.endEarly)).toHaveCount(0);
});

test('CloseSprint_WhenMounted_ShouldOpenNothingAndReadNothing', async ({ mount, page }) => {
  const reads: string[] = [];
  await stub(page, () => {
    reads.push('rollover');
    return rollover();
  });
  await mountButton(mount);

  await expect(page.getByTestId(ids.closeSprint)).toHaveText('Close sprint');
  await expect(page.getByTestId(componentTestIds.dialog)).toHaveCount(0);
  expect(reads).toEqual([]);
});

test('CloseSprint_WhenAlreadyClosed_ShouldShowNoButton', async ({ mount, page }) => {
  await stub(page, () => rollover({ status: 'decided' }));
  await mountButton(mount, { ...ended, rolloverStatus: 'decided' });

  await expect(page.getByTestId(ids.closeSprint)).toHaveCount(0);
});

test('CloseSprint_WhenFuture_ShouldShowNoButton', async ({ mount, page }) => {
  await stub(page, () => rollover());
  await mountButton(mount, { ...ended, status: 'future' });

  await expect(page.getByTestId(ids.closeSprint)).toHaveCount(0);
});

test('CloseSprint_WhenNothingIsUnfinished_ShouldConfirmThenCloseWithNoMoves', async ({
  mount,
  page,
}) => {
  const posts: Posted[] = [];
  await stub(page, () => rollover({ items: [] }), posts);
  await openDialog(mount, page);

  await expect(page.getByTestId(ids.endConfirm)).toContainText(
    'Nothing is unfinished in Sprint 24',
  );
  await expect(page.getByTestId(ids.endRow)).toHaveCount(0);
  await expect(page.getByTestId(ids.endEarly)).toHaveCount(0);
  await page.getByTestId(ids.endApply).click();

  await expect(page.getByTestId(componentTestIds.dialog)).toHaveCount(0);
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('Closed Sprint 24');
  expect(posts).toEqual([{ moves: [], endToday: false }]);
});

test('CloseSprint_WhenStillRunning_ShouldSayTheEndDateMovesAndAskForIt', async ({
  mount,
  page,
}) => {
  const posts: Posted[] = [];
  await stub(page, () => rollover({ endsEarly: true }), posts);
  await openDialog(mount, page);

  await expect(page.getByTestId(ids.endEarly)).toContainText('moves its end date to today');
  await page.getByTestId(ids.endApply).click();

  await expect(page.getByTestId(componentTestIds.dialog)).toHaveCount(0);
  expect(posts[0]!.endToday).toBe(true);
});

test('CloseSprint_WhenStillRunningWithNothingUnfinished_ShouldSayTheEndDateMoves', async ({
  mount,
  page,
}) => {
  await stub(page, () => rollover({ items: [], endsEarly: true }));
  await openDialog(mount, page);

  await expect(page.getByTestId(ids.endConfirm)).toBeVisible();
  await expect(page.getByTestId(ids.endEarly)).toContainText('moves its end date to today');
});

test('CloseSprint_WhenTheRolloverCannotBeRead_ShouldSaySoAndCancelBack', async ({
  mount,
  page,
}) => {
  await page.route('**/api/sprints/s24/rollover', (route) =>
    route.fulfill({ status: 404, json: { error: 'There is no such sprint.', code: 'not_found' } }),
  );
  await openDialog(mount, page);

  await expect(page.getByTestId(ids.endError)).toContainText('could not be loaded');
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(componentTestIds.dialog)).toHaveCount(0);
});

test('CloseSprint_WhenMovedPastNextSprintCapacity_ShouldWarnButNotBlock', async ({
  mount,
  page,
}) => {
  await stub(page, () => rollover());
  await openDialog(mount, page);

  // 6 planned + 8 moving = 14 against 12.
  const after = page.getByTestId(ids.endAfter);
  await expect(after).toContainText('Sprint 25 after this: 14 / 12 pts');
  await expect(after).toContainText('2 over team capacity');
  await expect(after).toHaveAttribute('data-over', 'true');
  await expect(page.getByTestId(ids.endApply)).toBeEnabled();

  await page.getByTestId(ids.endDestination).first().selectOption('backlog');
  await expect(after).toContainText('9 / 12 pts');
  await expect(after).toContainText('3 pts of room left');
  await expect(after).toHaveAttribute('data-over', 'false');
});

test('CloseSprint_WhenSetAllIsPressed_ShouldChangeEveryRowAndTheTotal', async ({ mount, page }) => {
  await stub(page, () => rollover());
  await openDialog(mount, page);

  await page.getByTestId(ids.endSetAll).filter({ hasText: 'Top of backlog' }).click();
  for (const select of await page.getByTestId(ids.endDestination).all()) {
    await expect(select).toHaveValue('backlog');
  }
  await expect(page.getByTestId(ids.endAfter)).toContainText('Sprint 25 after this: 6 / 12 pts');
  await page.getByTestId(ids.endSetAll).filter({ hasText: 'Leave in Sprint 24' }).click();
  await expect(page.getByTestId(ids.endDestination).last()).toHaveValue('stay');
  await page.getByTestId(ids.endSetAll).filter({ hasText: 'Sprint 25' }).first().click();
  await expect(page.getByTestId(ids.endAfter)).toContainText('14 / 12 pts');
});

test('CloseSprint_WhenNoCapacityIsEntered_ShouldNotWarn', async ({ mount, page }) => {
  await stub(page, () => rollover({ nextSprintCapacity: null }));
  await openDialog(mount, page);

  await expect(page.getByTestId(ids.endAfter)).toContainText('No capacity is entered');
  await expect(page.getByTestId(ids.endAfter)).toHaveAttribute('data-over', 'false');
});

test('CloseSprint_WhenThereIsNoNextSprint_ShouldOfferTheBacklogAndStayOnly', async ({
  mount,
  page,
}) => {
  const noNext = rollover({
    nextSprint: null,
    nextSprintCapacity: null,
    nextSprintPoints: 0,
    items: [{ ...unfinished('SHIP-129', 5, 0, 0), defaultTargetSprintId: null }],
  });
  await stub(page, () => noNext);
  await openDialog(mount, page);

  const select = page.getByTestId(ids.endDestination);
  await expect(select).toHaveValue('backlog');
  await expect(select.locator('option')).toHaveText([
    'Back to top of backlog',
    'Leave in Sprint 24',
  ]);
  await expect(page.getByTestId(ids.endAfter)).toHaveCount(0);
});

test('CloseSprint_WhenApplied_ShouldPostTheStoryMovesAndClose', async ({ mount, page }) => {
  const posts: Posted[] = [];
  await stub(page, () => rollover(), posts);
  await openDialog(mount, page);
  await page.getByTestId(ids.endDestination).last().selectOption('backlog');

  await page.getByTestId(ids.endApply).click();

  await expect(page.getByTestId(componentTestIds.dialog)).toHaveCount(0);
  await expect(page.getByTestId(componentTestIds.toast)).toContainText(
    'Closed Sprint 24 and moved 2 items out',
  );
  expect(posts).toHaveLength(1);
  expect(posts[0]!.moves).toEqual([
    { workItemId: 'SHIP-129', targetSprintId: 's25' },
    { workItemId: 'SHIP-144', targetSprintId: null },
  ]);
});

test('CloseSprint_WhenAnItemIsLeftInTheSprint_ShouldNotMoveItOrItsTasks', async ({
  mount,
  page,
}) => {
  const posts: Posted[] = [];
  await stub(page, () => rollover(), posts);
  await openDialog(mount, page);
  await page.getByTestId(ids.endDestination).first().selectOption('stay');

  await page.getByTestId(ids.endApply).click();

  await expect(page.getByTestId(componentTestIds.toast)).toContainText('moved 1 item out');
  expect(posts[0]!.moves).toEqual([{ workItemId: 'SHIP-144', targetSprintId: 's25' }]);
});

test('CloseSprint_WhenTheApplyIsRefused_ShouldSayWhyAndKeepTheDialog', async ({ mount, page }) => {
  await stub(page, () => rollover());
  await page.route('**/api/sprints/s24/rollover', (route) =>
    route.request().method() === 'POST'
      ? route.fulfill({
          status: 409,
          json: {
            error: 'These items changed since the rollover was loaded: SHIP-129.',
            code: 'conflict',
          },
        })
      : route.fulfill({ json: rollover() }),
  );
  await openDialog(mount, page);

  await page.getByTestId(ids.endApply).click();

  await expect(page.getByTestId(ids.endError)).toContainText('These items changed');
  await expect(page.getByTestId(componentTestIds.dialog)).toBeVisible();
});

test('CloseSprint_WhenCancelled_ShouldCloseTheDialogMoveNothingAndKeepTheButton', async ({
  mount,
  page,
}) => {
  const posts: Posted[] = [];
  await stub(page, () => rollover(), posts);
  await openDialog(mount, page);

  await page.getByTestId(ids.endCancel).click();

  await expect(page.getByTestId(componentTestIds.dialog)).toHaveCount(0);
  await expect(page.getByTestId(ids.closeSprint)).toBeFocused();
  expect(posts).toEqual([]);
  expect(await page.evaluate(() => window.localStorage.length)).toBe(0);
});

test('CloseSprint_WhenEscIsPressed_ShouldCancel', async ({ mount, page }) => {
  await stub(page, () => rollover());
  await openDialog(mount, page);
  await expect(page.getByTestId(componentTestIds.dialog)).toBeVisible();

  await page.keyboard.press('Escape');

  await expect(page.getByTestId(componentTestIds.dialog)).toHaveCount(0);
  await expect(page.getByTestId(ids.closeSprint)).toBeVisible();
});

test('CloseSprint_WhenAnotherMemberClosesItWhileOpen_ShouldShowTheDoneState', async ({
  mount,
  page,
}) => {
  let decided = false;
  await stub(page, () => (decided ? rollover({ status: 'decided', items: [] }) : rollover()));
  await openDialog(mount, page);
  await expect(page.getByTestId(ids.endApply)).toBeVisible();

  decided = true;
  // A teammate's event refetches the rollover; a refocus refetches it the same way here (the harness has no hub).
  await page.evaluate(() =>
    document.dispatchEvent(new Event('visibilitychange', { bubbles: true })),
  );
  await expect(page.getByTestId(ids.endDone)).toBeVisible();
  await expect(page.getByTestId(ids.endApply)).toHaveCount(0);
  await page.getByTestId(ids.endDoneClose).click();
  await expect(page.getByTestId(componentTestIds.dialog)).toHaveCount(0);
});
