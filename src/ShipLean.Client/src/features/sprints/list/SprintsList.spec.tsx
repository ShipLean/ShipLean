import type { Page } from 'playwright';
import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '../../../api/projects';
import type { SprintCapacityDto, SprintDto } from '../../../api/sprints';
import { componentTestIds } from '../../../components/testIds';
import { sprintsTestIds as ids } from '../testIds';
import { SprintsList } from './SprintsList';

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

// Deliberately out of order: the list puts the current sprint first, then the future, then the past (latest first).
const sprints = [
  sprintOf(22, 'past'),
  sprintOf(24, 'current', 'Check out with a saved card'),
  sprintOf(23, 'past'),
  sprintOf(26, 'future'),
  sprintOf(25, 'future'),
];
sprints[0]!.startDate = '2026-08-24';
sprints[2]!.startDate = '2026-09-07';
sprints[4]!.startDate = '2026-10-19';
sprints[3]!.startDate = '2026-11-02';

const figures: Record<string, { committed: number; completed: number }> = {
  s22: { committed: 18, completed: 16 },
  s23: { committed: 20, completed: 21 },
  s24: { committed: 22, completed: 9 },
  s25: { committed: 7, completed: 0 },
  s26: { committed: 0, completed: 0 },
};

const capacityOf = (sprintId: string, teamTotal: number | null): SprintCapacityDto => ({
  sprintId,
  members:
    teamTotal === null
      ? []
      : [
          {
            user: { id: 'u1', name: 'Priya', initials: 'P', avatarColour: 'blue', photoUrl: null },
            points: teamTotal,
            note: null,
          },
        ],
  teamTotal: teamTotal ?? 0,
});

async function stub(
  page: Page,
  capacity: Record<string, number | null>,
  override: number | null = null,
) {
  await page.route('**/api/projects/p1/velocity', (route) =>
    route.fulfill({ json: { window: 3, sprints: [], average: 19, override } }),
  );
  await page.route('**/api/sprints/*/metrics', (route) => {
    const id = route.request().url().split('/').at(-2)!;
    return route.fulfill({
      json: { sprintId: id, ...figures[id], remaining: 0, burndown: [] },
    });
  });
  await page.route('**/api/sprints/*/capacity', (route) => {
    const id = route.request().url().split('/').at(-2)!;
    return route.fulfill({ json: capacityOf(id, capacity[id] ?? null) });
  });
}

const rowOf = (page: Page, name: string) =>
  page
    .getByTestId(ids.listRow)
    .filter({ has: page.getByTestId(ids.rowLink).filter({ hasText: new RegExp(`^${name}$`) }) });

test('SprintsList_WhenLoaded_ShouldListCurrentThenFutureThenPast', async ({ mount, page }) => {
  await stub(page, {});
  await mount(<SprintsList project={project} sprints={sprints} />);

  await expect(page.getByTestId(ids.rowLink)).toHaveText([
    'Sprint 24',
    'Sprint 25',
    'Sprint 26',
    'Sprint 23',
    'Sprint 22',
  ]);
});

test('SprintsList_WhenSprintShown_ShouldShowDatesStatusGoalAndLinkToItsBacklog', async ({
  mount,
  page,
}) => {
  await stub(page, {});
  await mount(<SprintsList project={project} sprints={sprints} />);

  const current = rowOf(page, 'Sprint 24');
  await expect(current).toContainText('24 Oct – 35 Oct');
  await expect(current.getByTestId(componentTestIds.chip)).toHaveText('Current');
  await expect(current.getByTestId(componentTestIds.chip)).toHaveAttribute('data-tone', 'accent');
  await expect(current.getByTestId(ids.rowGoal)).toHaveText('Check out with a saved card');
  await expect(current.getByTestId(ids.rowLink)).toHaveAttribute(
    'href',
    '/p/SHIP/sprints/s24/backlog',
  );
  await expect(rowOf(page, 'Sprint 23').getByTestId(componentTestIds.chip)).toHaveText('Past');
  await expect(rowOf(page, 'Sprint 25').getByTestId(componentTestIds.chip)).toHaveText('Future');
  await expect(rowOf(page, 'Sprint 25').getByTestId(ids.rowGoal)).toHaveText('No goal yet');
});

test('SprintsList_WhenSprintsHavePoints_ShouldShowCommittedAndCompletedPerStatus', async ({
  mount,
  page,
}) => {
  await stub(page, {});
  await mount(<SprintsList project={project} sprints={sprints} />);

  await expect(rowOf(page, 'Sprint 23').getByTestId(ids.rowCommitted)).toHaveText('20');
  await expect(rowOf(page, 'Sprint 23').getByTestId(ids.rowCompleted)).toHaveText('21');
  await expect(rowOf(page, 'Sprint 24').getByTestId(ids.rowCommitted)).toHaveText('22');
  await expect(rowOf(page, 'Sprint 24').getByTestId(ids.rowCompleted)).toHaveText('9 so far');
  await expect(rowOf(page, 'Sprint 25').getByTestId(ids.rowCommitted)).toHaveText('7');
  await expect(rowOf(page, 'Sprint 25').getByTestId(ids.rowCompleted)).toHaveText('—');
  await expect(rowOf(page, 'Sprint 26').getByTestId(ids.rowCommitted)).toHaveText('—');
});

test('SprintsList_WhenOnlySomeSprintsHaveCapacity_ShouldShowTeamTotalOrVelocity', async ({
  mount,
  page,
}) => {
  await stub(page, { s24: 21, s25: 17 });
  await mount(<SprintsList project={project} sprints={sprints} />);

  await expect(rowOf(page, 'Sprint 24').getByTestId(ids.rowCapacity)).toHaveText('21');
  await expect(rowOf(page, 'Sprint 25').getByTestId(ids.rowCapacity)).toHaveText('17');
  await expect(rowOf(page, 'Sprint 26').getByTestId(ids.rowCapacity)).toHaveText('19 · velocity');
  await expect(rowOf(page, 'Sprint 22').getByTestId(ids.rowCapacity)).toHaveText('19 · velocity');
});

test('SprintsList_WhenNoCapacityAnywhere_ShouldLabelEverySprintWithVelocity', async ({
  mount,
  page,
}) => {
  await stub(page, {}, 25);
  await mount(<SprintsList project={project} sprints={sprints} />);

  const cells = page.getByTestId(ids.rowCapacity);
  await expect(cells).toHaveCount(5);
  await expect(cells).toHaveText(Array<string>(5).fill('25 · velocity'));
});

test('SprintsList_WhenAMemberLeftACapacityNote_ShouldUnderlineTheTotalWithTheNoteOnHover', async ({
  mount,
  page,
}) => {
  await stub(page, { s24: 21, s25: 17 });
  await page.route('**/api/sprints/s24/capacity', (route) =>
    route.fulfill({
      json: {
        ...capacityOf('s24', 21),
        members: [{ ...capacityOf('s24', 21).members[0]!, note: 'On leave Thursday' }],
      },
    }),
  );
  await mount(<SprintsList project={project} sprints={sprints} />);

  const note = rowOf(page, 'Sprint 24').getByTestId(ids.rowCapacityNote);
  await expect(note).toHaveText('21');
  await expect(note).toHaveAttribute('title', 'Priya: On leave Thursday');
  await expect(rowOf(page, 'Sprint 25').getByTestId(ids.rowCapacityNote)).toHaveCount(0);
});
