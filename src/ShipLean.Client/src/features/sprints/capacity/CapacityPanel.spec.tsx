import type { Page } from 'playwright';
import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '../../../api/projects';
import type {
  MemberCapacityDto,
  SprintCapacityDto,
  SprintCapacityPut,
  SprintDto,
  VelocityDto,
} from '../../../api/sprints';
import { ToastHost } from '../../../components/ToastHost';
import { CapacitySlot } from '../../../slots/CapacitySlot';
import { componentTestIds } from '../../../components/testIds';
import { sprintsTestIds as ids } from '../testIds';
import { CapacityPanel } from './CapacityPanel';

const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const sprintOf = (n: number, startDate: string, status: SprintDto['status']) =>
  ({ id: `s${n}`, name: `Sprint ${n}`, startDate, status }) as SprintDto;
const sprints = [
  sprintOf(23, '2026-09-21', 'past'),
  sprintOf(24, '2026-10-05', 'current'),
  sprintOf(25, '2026-10-19', 'future'),
];
const sprint = sprints[1]!;

const user = (id: string, name: string) => ({
  id,
  name,
  initials: name.slice(0, 2),
  avatarColour: '#336699',
  photoUrl: null,
});
const people = [user('u1', 'Priya'), user('u2', 'Tom'), user('u3', 'Mei')];
const members = people.map((u) => ({ user: u, email: '', role: 'member', status: 'active' }));
const entry = (index: number, points: number, note: string | null = null): MemberCapacityDto => ({
  user: people[index]!,
  points,
  note,
});
const capacityOf = (sprintId: string, rows: MemberCapacityDto[]): SprintCapacityDto => ({
  sprintId,
  members: rows,
  teamTotal: rows.reduce((sum, row) => sum + row.points, 0),
});
const velocity: VelocityDto = { window: 3, sprints: [], average: 20, override: null };

interface Stub {
  puts: SprintCapacityPut[];
  /** What the server holds for the sprint from now on (another user's save). */
  setCurrent: (capacity: SprintCapacityDto) => void;
}

async function stubApi(
  page: Page,
  options: {
    current?: SprintCapacityDto;
    previous?: SprintCapacityDto;
    committed?: number;
    velocity?: VelocityDto;
    putStatus?: number;
    previousStatus?: number;
  } = {},
): Promise<Stub> {
  let current = options.current ?? capacityOf('s24', []);
  const stub: Stub = {
    puts: [],
    setCurrent: (capacity) => {
      current = capacity;
    },
  };
  const previous = options.previous ?? capacityOf('s23', []);
  await page.route('**/api/projects/p1/members', (r) => r.fulfill({ json: members }));
  await page.route('**/api/projects/p1/velocity', (r) =>
    r.fulfill({ json: options.velocity ?? velocity }),
  );
  await page.route('**/api/sprints/s24/metrics', (r) =>
    r.fulfill({
      json: {
        sprintId: 's24',
        committed: options.committed ?? 18,
        completed: 0,
        remaining: 0,
        burndown: [],
      },
    }),
  );
  await page.route('**/api/sprints/s23/capacity', (r) =>
    options.previousStatus === undefined
      ? r.fulfill({ json: previous })
      : r.fulfill({
          status: options.previousStatus,
          json: { error: 'Boom', code: 'server_error' },
        }),
  );
  await page.route('**/api/sprints/s25/metrics', (r) =>
    r.fulfill({
      json: { sprintId: 's25', committed: 0, completed: 0, remaining: 0, burndown: [] },
    }),
  );
  await page.route('**/api/sprints/s25/capacity', (r) =>
    r.fulfill({ json: capacityOf('s25', [entry(0, 2)]) }),
  );
  await page.route('**/api/sprints/s24/capacity', async (r) => {
    if (r.request().method() !== 'PUT') return r.fulfill({ json: current });
    if (options.putStatus !== undefined) {
      return r.fulfill({
        status: options.putStatus,
        json: {
          error: 'Capacity can only be entered for members of the project.',
          code: 'validation',
        },
      });
    }
    const body = r.request().postDataJSON() as SprintCapacityPut;
    stub.puts.push(body);
    current = capacityOf(
      's24',
      body.members.map((m) => ({
        user: people.find((p) => p.id === m.userId)!,
        points: m.points,
        note: m.note,
      })),
    );
    return r.fulfill({ json: current });
  });
  return stub;
}

const refetchOnFocus = (page: Page) =>
  page.evaluate(() => window.dispatchEvent(new Event('visibilitychange')));

const points = (page: Page, index: number) => page.getByTestId(ids.capacityPoints).nth(index);

test('Capacity_WhenMembersEnterPoints_ShouldSumIntoTheTeamTotalOnly', async ({ mount, page }) => {
  await stubApi(page, { committed: 18 });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(3);
  await expect(page.getByTestId(ids.capacityTotal)).toHaveText('— (velocity 20)');
  await expect(page.getByTestId(ids.capacityKpiCapacity)).toHaveText('20');

  await points(page, 0).fill('6');
  await points(page, 1).fill('5');
  await points(page, 2).fill('4.5');

  await expect(page.getByTestId(ids.capacityTotal)).toHaveText('15.5');
  await expect(page.getByTestId(ids.capacityKpiCapacity)).toHaveText('15.5');
  await expect(page.getByTestId(ids.capacityKpiCommitted)).toHaveText('18');
  await expect(page.getByTestId(ids.capacityRoom)).toContainText('2.5 pts over');
  await expect(page.getByTestId(componentTestIds.capacityBar)).toHaveAttribute('data-tone', 'over');
  // Only the team figure is compared: no row carries a comparison of its own.
  await expect(page.getByTestId(ids.capacityRow).first().getByTestId(ids.capacityRoom)).toHaveCount(
    0,
  );
});

test('Capacity_WhenNothingEntered_ShouldPlanAgainstVelocity', async ({ mount, page }) => {
  await stubApi(page, { committed: 12 });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(page.getByTestId(ids.capacityCallout)).toContainText('No capacity entered');
  await expect(page.getByTestId(ids.capacityRoom)).toContainText('8 pts');
  await expect(page.getByTestId(componentTestIds.capacityBar)).toHaveAttribute(
    'data-tone',
    'velocity',
  );
});

test('Capacity_WhenFarFromVelocity_ShouldWarnEitherWayButNotAtTheEdge', async ({ mount, page }) => {
  await stubApi(page);
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  const callout = page.getByTestId(ids.capacityCallout);

  await points(page, 0).fill('18');
  await expect(callout).toHaveAttribute('data-tone', 'info');
  await expect(callout).toContainText('In line with velocity');
  await points(page, 0).fill('22');
  await expect(callout).toHaveAttribute('data-tone', 'info');

  await points(page, 0).fill('23');
  await expect(callout).toHaveAttribute('data-tone', 'warn');
  await expect(callout).toContainText('15% above velocity');
  await points(page, 0).fill('15');
  await expect(callout).toHaveAttribute('data-tone', 'warn');
  await expect(callout).toContainText('25% below velocity');
});

test('Capacity_WhenProjectHasAnOverride_ShouldCompareWithIt', async ({ mount, page }) => {
  await stubApi(page, { velocity: { ...velocity, override: 30 } });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await points(page, 0).fill('21');
  await expect(page.getByTestId(ids.capacityKpiVelocity)).toHaveText('30');
  await expect(page.getByTestId(ids.capacityCallout)).toContainText('30% below velocity');
  await expect(
    page.getByTestId(ids.capacityKpiVelocity).locator('xpath=following-sibling::*'),
  ).toHaveText('Velocity (override)');
});

test('Capacity_WhenNoOverride_ShouldLabelTheAverageWithItsWindow', async ({ mount, page }) => {
  await stubApi(page);
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(
    page.getByTestId(ids.capacityKpiVelocity).locator('xpath=following-sibling::*'),
  ).toHaveText('Velocity (last 3)');
});

test('Capacity_WhenCommittedIsOverByAFraction_ShouldShowARoundedFigure', async ({
  mount,
  page,
}) => {
  await stubApi(page, { committed: 13, velocity: { ...velocity, average: 10.1 } });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(page.getByTestId(ids.capacityRoom)).toContainText('2.9 pts over');
});

test('Capacity_WhenSprintSwitchedAfterAnEdit_ShouldDropTheEditAndDisableSave', async ({
  mount,
  page,
}) => {
  const stub = await stubApi(page);
  const slot = await mount(<CapacitySlot project={project} sprint={sprint} sprints={sprints} />);
  await points(page, 0).fill('7');
  await expect(page.getByTestId(ids.capacitySave)).toBeEnabled();

  await slot.update(<CapacitySlot project={project} sprint={sprints[2]!} sprints={sprints} />);

  await expect(points(page, 0)).toHaveValue('2');
  await expect(page.getByTestId(ids.capacitySave)).toBeDisabled();
  expect(stub.puts).toEqual([]);
});

test('Capacity_WhenATeammateSavesWhileFormIsDirty_ShouldKeepTheirRowInTheSave', async ({
  mount,
  page,
}) => {
  const stub = await stubApi(page);
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await points(page, 0).fill('3');

  stub.setCurrent(capacityOf('s24', [entry(1, 8)]));
  await refetchOnFocus(page);

  await expect(points(page, 1)).toHaveValue('8');
  await expect(page.getByTestId(ids.capacityTotal)).toHaveText('11');
  await expect(page.getByTestId(ids.capacityConflict)).toHaveCount(0);
  await page.getByTestId(ids.capacitySave).click();
  await expect.poll(() => stub.puts.length).toBe(1);
  expect(stub.puts[0]).toEqual({
    members: [
      { userId: 'u1', points: 3, note: null },
      { userId: 'u2', points: 8, note: null },
    ],
  });
});

test('Capacity_WhenATeammateChangesARowYouChanged_ShouldSayWhoAndKeepYours', async ({
  mount,
  page,
}) => {
  const stub = await stubApi(page, { current: capacityOf('s24', [entry(1, 5)]) });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await points(page, 1).fill('4');
  await expect(page.getByTestId(ids.capacityConflict)).toHaveCount(0);

  stub.setCurrent(capacityOf('s24', [entry(1, 8)]));
  await refetchOnFocus(page);

  await expect(page.getByTestId(ids.capacityConflict)).toContainText('Tom changed capacity');
  await expect(points(page, 1)).toHaveValue('4');
  await page.getByTestId(ids.capacitySave).click();
  await expect.poll(() => stub.puts.length).toBe(1);
  expect(stub.puts[0]).toEqual({ members: [{ userId: 'u2', points: 4, note: null }] });
});

test('Capacity_WhenCopiedFromASprintWithAFormerMember_ShouldNotCountThemInTheTotalOrSaveThem', async ({
  mount,
  page,
}) => {
  const former = { user: user('u9', 'Former'), points: 9, note: null };
  const stub = await stubApi(page, {
    previous: capacityOf('s23', [entry(0, 6), entry(1, 5), entry(2, 4), former]),
  });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await page.getByTestId(ids.capacityCopy).click();
  await expect(page.getByTestId(ids.capacityTotal)).toHaveText('15');
  await expect(page.getByTestId(ids.capacityKpiCapacity)).toHaveText('15');
  await page.getByTestId(ids.capacitySave).click();
  await expect.poll(() => stub.puts.length).toBe(1);
  expect(stub.puts[0]?.members.map((m) => m.userId)).toEqual(['u1', 'u2', 'u3']);
});

test('Capacity_WhenThePreviousCapacityFailsToLoad_ShouldDisableCopyAndSayWhy', async ({
  mount,
  page,
}) => {
  await stubApi(page, { previousStatus: 500 });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(page.getByTestId(ids.capacityCopy)).toHaveAttribute(
    'title',
    'The capacity of the sprint before could not be loaded',
  );
  await expect(page.getByTestId(ids.capacityCopy)).toBeDisabled();
});

test('Capacity_WhenCopiedAndCleared_ShouldToastBoth', async ({ mount, page }) => {
  await stubApi(page, { previous: capacityOf('s23', [entry(0, 6)]) });
  await mount(
    <>
      <CapacityPanel project={project} sprint={sprint} sprints={sprints} />
      <ToastHost />
    </>,
  );
  await page.getByTestId(ids.capacityCopy).click();
  await expect(page.getByTestId(componentTestIds.toast)).toHaveText(
    /Copied capacity from Sprint 23/,
  );
  await page.getByTestId(ids.capacityClear).click();
  await expect(page.getByTestId(componentTestIds.toast).last()).toHaveText(
    /Cleared\. Sprint 24 now plans against velocity/,
  );
});

test('Capacity_WhenCopied_ShouldFillEveryRowFromThePreviousSprintThenSave', async ({
  mount,
  page,
}) => {
  const stub = await stubApi(page, {
    current: capacityOf('s24', [entry(0, 1)]),
    previous: capacityOf('s23', [entry(0, 6, 'Off Fri'), entry(1, 5), entry(2, 4)]),
  });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(points(page, 0)).toHaveValue('1');
  await expect(page.getByTestId(ids.capacitySave)).toBeDisabled();

  await page.getByTestId(ids.capacityCopy).click();

  await expect(page.getByTestId(ids.capacityCopy)).toHaveText('Copy from Sprint 23');
  await expect(points(page, 0)).toHaveValue('6');
  await expect(points(page, 1)).toHaveValue('5');
  await expect(points(page, 2)).toHaveValue('4');
  await expect(page.getByTestId(ids.capacityNote).first()).toHaveValue('Off Fri');
  await expect(page.getByTestId(ids.capacityTotal)).toHaveText('15');
  expect(stub.puts).toEqual([]);

  await page.getByTestId(ids.capacitySave).click();
  await expect.poll(() => stub.puts.length).toBe(1);
  expect(stub.puts[0]).toEqual({
    members: [
      { userId: 'u1', points: 6, note: 'Off Fri' },
      { userId: 'u2', points: 5, note: null },
      { userId: 'u3', points: 4, note: null },
    ],
  });
  await expect(page.getByTestId(ids.capacitySave)).toBeDisabled();
});

test('Capacity_WhenPreviousSprintHasNoEntries_ShouldDisableCopy', async ({ mount, page }) => {
  await stubApi(page);
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(3);
  await expect(page.getByTestId(ids.capacityCopy)).toBeDisabled();
});

test('Capacity_WhenFirstSprint_ShouldOfferNoNamedCopy', async ({ mount, page }) => {
  await stubApi(page);
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={[sprint]} />);
  await expect(page.getByTestId(ids.capacityCopy)).toHaveText('Copy from previous sprint');
  await expect(page.getByTestId(ids.capacityCopy)).toBeDisabled();
});

test('Capacity_WhenCleared_ShouldSaveAnEmptySetAndPlanAgainstVelocity', async ({ mount, page }) => {
  const stub = await stubApi(page, { current: capacityOf('s24', [entry(0, 6), entry(1, 5)]) });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(page.getByTestId(ids.capacityTotal)).toHaveText('11');

  await page.getByTestId(ids.capacityClear).click();
  await expect(page.getByTestId(ids.capacityTotal)).toHaveText('— (velocity 20)');
  await page.getByTestId(ids.capacitySave).click();

  await expect.poll(() => stub.puts.length).toBe(1);
  expect(stub.puts[0]).toEqual({ members: [] });
});

test('Capacity_WhenSavedBlankAndNotesOnly_ShouldSendOnlyRowsWithPoints', async ({
  mount,
  page,
}) => {
  const stub = await stubApi(page);
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await page.getByTestId(ids.capacityNote).nth(1).fill('Away all sprint');
  await points(page, 2).fill('0');
  await page.getByTestId(ids.capacitySave).click();
  await expect.poll(() => stub.puts.length).toBe(1);
  expect(stub.puts[0]).toEqual({ members: [{ userId: 'u3', points: 0, note: null }] });
});

test('Capacity_WhenPointsInvalid_ShouldNameTheMemberAndNotSave', async ({ mount, page }) => {
  const stub = await stubApi(page);
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await points(page, 1).fill('-3');
  await page.getByTestId(ids.capacitySave).click();
  await expect(page.getByTestId(ids.capacityError)).toContainText('Tom');
  expect(stub.puts).toEqual([]);
  await points(page, 1).fill('3');
  await expect(page.getByTestId(ids.capacityError)).toHaveCount(0);
});

test('Capacity_WhenSaveRefused_ShouldShowTheServersMessageAndKeepTheForm', async ({
  mount,
  page,
}) => {
  await stubApi(page, { putStatus: 400 });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await points(page, 0).fill('5');
  await page.getByTestId(ids.capacitySave).click();
  await expect(page.getByTestId(ids.capacityError)).toHaveText(
    'Capacity can only be entered for members of the project.',
  );
  await expect(points(page, 0)).toHaveValue('5');
  await expect(page.getByTestId(ids.capacitySave)).toBeEnabled();
});

test('Capacity_WhenMembersFailToLoad_ShouldSayTheCapacityCouldNotBeLoaded', async ({
  mount,
  page,
}) => {
  await stubApi(page);
  await page.route('**/api/projects/p1/members', (r) =>
    r.fulfill({ status: 500, json: { error: 'Boom', code: 'server_error' } }),
  );
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(page.getByTestId(ids.capacityLoadError)).toBeVisible();
});
