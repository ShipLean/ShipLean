import { expect, test } from '@playwright/experimental-ct-react';
import type { TaskboardViewDto } from '@shiplean/core/api/views';
import type { MemberDto } from '@shiplean/core/api/projects';
import type { UserRef } from '@shiplean/core/api/refs';
import { TaskboardSlot } from '../../../slots/TaskboardSlot';
import { taskboardTestIds as ids } from '../testIds';
import { touchTestIds } from '../../../touch/testIds';
import { PhoneGridHarness } from './PhoneGridHarness';
import { PRIYA, PROJECT, SPRINT, SPRINTS, TOM, VIEW } from './fixtures';
import { stubTaskboard } from './stubApi';

// The sprint Taskboard grouped by People on a phone (SHIP-725 D16): everyone on the project is a lane. Runs in the CT phone project.

const route = { hooksConfig: { route: '/p/SHIP/sprints/s24/taskboard' } };
const slot = <TaskboardSlot project={PROJECT} sprint={SPRINT} sprints={SPRINTS} />;
const THEMES = ['light', 'dark'] as const;

const MEI: UserRef = {
  id: 'u-mei',
  name: 'Mei Lin',
  initials: 'ML',
  avatarColour: '#8a5ab5',
  photoUrl: null,
};
const member = (user: UserRef): MemberDto => ({
  user,
  email: `${user.id}@example.com`,
  role: 'member',
  status: 'active',
});
const MEMBERS = [PRIYA, TOM, MEI].map(member);

type Page = Parameters<Parameters<typeof test>[2]>[0]['page'];

async function arrange(page: Page) {
  await page.route(`**/api/projects/${PROJECT.id}/members`, (r) =>
    r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(MEMBERS) }),
  );
  await stubTaskboard(page, VIEW, { saved: { groupBy: 'people' }, holdLaterReads: true });
}

test('PhonePeople_WhenPersonHasNoTasks_ShouldShowEmptyLane', async ({ mount, page }) => {
  await arrange(page);
  await mount(slot, route);

  const first = page.getByTestId('touch-pager-page').first();
  const lanes = first.getByTestId(ids.lane);
  await expect(lanes).toHaveCount(4);
  await expect(lanes.first()).toHaveAttribute('data-row', 'unassigned');
  const mei = lanes.filter({ hasText: 'Mei Lin' });
  await expect(mei.getByTestId(ids.phonePersonEmpty)).toHaveText('No tasks in this sprint');
  await expect(mei.getByTestId(ids.rowCount)).toHaveText('0 tasks');
  // Priya has tasks, only none on this page: the usual None.
  await expect(lanes.filter({ hasText: 'Priya Shah' }).getByTestId(ids.laneNone)).toHaveText(
    'None',
  );
});

// The board with a task in another state, as the server answers once the move is written.
type Cells = { stateId: string; tasks: { id: string }[] }[];
function movedTo(view: TaskboardViewDto, taskId: string, stateId: string): TaskboardViewDto {
  const move = <T extends { cells: Cells }>(row: T): T => {
    const task = row.cells.flatMap((c) => c.tasks).find((t) => t.id === taskId);
    if (!task) return row;
    return {
      ...row,
      cells: row.cells.map((c) => ({
        ...c,
        tasks: [
          ...c.tasks.filter((t) => t.id !== taskId),
          ...(c.stateId === stateId ? [task] : []),
        ],
      })),
    };
  };
  return {
    ...view,
    rows: view.rows.map(move),
    peopleRows: view.peopleRows.map(move),
  } as TaskboardViewDto;
}

test('PhoneGrid_WhenOwnMoveLands_ShouldNotPulseAChip', async ({ mount, page }) => {
  // The refetch after the move answers with the task in its new state: that is the move you made, not a teammate's.
  const server = await stubTaskboard(page, VIEW);
  const first = VIEW.peopleRows[0].cells.flatMap((c) => c.tasks)[0];
  const landed = movedTo(VIEW, first.id, 't-active');
  let reads = 0;
  await page.route('**/api/sprints/*/taskboard', async (r) => {
    reads += 1;
    const body = JSON.stringify(reads > 1 ? landed : VIEW);
    await r.fulfill({ status: 200, contentType: 'application/json', body });
  });
  await mount(slot, route);
  // A pulse plays for under a second, so watch every chip for the class instead of looking once at the end.
  await page.evaluate(() => {
    const seen: string[] = [];
    Object.assign(window, { pulsed: seen });
    new MutationObserver((records) => {
      for (const r of records)
        if ((r.target as Element).classList.contains('is-pulsing')) seen.push('pulse');
    }).observe(document.body, { subtree: true, attributes: true, attributeFilter: ['class'] });
  });

  const card = page
    .getByTestId(ids.card)
    .and(page.locator(`[data-key="${first.key}"]`))
    .first();
  await card.getByTestId(ids.cardActions).tap();
  await page.getByTestId(touchTestIds.sheet).locator(`[data-state-id="t-active"]`).tap();

  await expect.poll(() => server.moves.length).toBe(1);
  await expect.poll(() => reads).toBe(2);
  await expect(page.getByTestId(touchTestIds.pagerChip)).toHaveCount(3);
  expect(await page.evaluate(() => (window as unknown as { pulsed: string[] }).pulsed)).toEqual([]);
});

test('PhonePeople_WhenFeatureBoard_ShouldNotRequestMembers', async ({ mount, page }) => {
  const requested: string[] = [];
  await page.route('**/api/projects/*/members', async (r) => {
    requested.push(r.request().url());
    await r.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
  });
  await mount(<PhoneGridHarness board="feature" grouping="people" />, {
    hooksConfig: { route: '/p/SHIP/items/SHIP-100/board' },
  });

  await expect(page.getByTestId(ids.lane).first()).toBeVisible();
  expect(requested).toEqual([]);
});

for (const theme of THEMES) {
  test(`PhonePeople_WhenEveryoneShown_ShouldMatchTheScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await arrange(page);
    await mount(slot, route);

    const first = page.getByTestId('touch-pager-page').first();
    await expect(first.getByTestId(ids.phonePersonEmpty)).toBeVisible();
    await expect(first).toHaveScreenshot(`taskboard-people-phone-${theme}.png`);
  });
}
