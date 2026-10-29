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

const project = { id: 'p1', key: 'SHIP', role: 'member' } as ProjectDto;
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
// Points are what the server stored; the tests that save check the days and velocity that go out.
const entry = (
  index: number,
  points: number,
  days: number | null = null,
  note: string | null = null,
): MemberCapacityDto => ({ user: people[index]!, days, points, note });
const capacityOf = (
  sprintId: string,
  rows: MemberCapacityDto[],
  extras: Partial<SprintCapacityDto> = {},
): SprintCapacityDto => ({
  sprintId,
  members: rows,
  teamTotal: rows.reduce((sum, row) => sum + row.points, 0),
  velocity: rows.length === 0 ? null : 30,
  velocityDefault: 25,
  workingDays: 10,
  defaultDays: 10,
  holidays: [],
  holidayCountry: null,
  ended: false,
  ...extras,
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
        days: m.days,
        points: 1,
        note: m.note,
      })),
      { velocity: body.velocity },
    );
    return r.fulfill({ json: current });
  });
  return stub;
}

const refetchOnFocus = (page: Page) =>
  page.evaluate(() => window.dispatchEvent(new Event('visibilitychange')));

const days = (page: Page, index: number) => page.getByTestId(ids.capacityDays).nth(index);
const rowPoints = (page: Page, index: number) => page.getByTestId(ids.capacityPoints).nth(index);
const velocityBox = (page: Page) => page.getByTestId(ids.capacityVelocity);
const save = (page: Page) => page.getByTestId(ids.capacitySave);

test('Capacity_WhenDaysChange_ShouldRecomputeEachShareAndTheTeamTotalLive', async ({
  mount,
  page,
}) => {
  await stubApi(page, {
    current: capacityOf('s24', [entry(0, 10), entry(1, 10), entry(2, 10)]),
    committed: 18,
  });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(3);
  await expect(velocityBox(page)).toHaveValue('30');
  await expect(rowPoints(page, 0)).toHaveText('10');
  await expect(page.getByTestId(ids.capacityTotal)).toHaveText('30');

  await days(page, 0).fill('5');
  await days(page, 1).fill('7.5');

  await expect(rowPoints(page, 0)).toHaveText('5');
  await expect(rowPoints(page, 1)).toHaveText('7.5');
  await expect(rowPoints(page, 2)).toHaveText('10');
  await expect(page.getByTestId(ids.capacityTotal)).toHaveText('22.5');
  await expect(page.getByTestId(ids.capacityKpiCapacity)).toHaveText('22.5');
  await expect(page.getByTestId(ids.capacityKpiCommitted)).toHaveText('18');
  await expect(page.getByTestId(ids.capacityRoom)).toContainText('4.5 pts');
  await expect(page.getByTestId(componentTestIds.capacityBar)).toHaveAttribute(
    'data-tone',
    'normal',
  );
  // Only the team figure is compared: no row carries a comparison of its own.
  await expect(page.getByTestId(ids.capacityRow).first().getByTestId(ids.capacityRoom)).toHaveCount(
    0,
  );

  await velocityBox(page).fill('24');
  await expect(rowPoints(page, 2)).toHaveText('8');
  await expect(page.getByTestId(ids.capacityTotal)).toHaveText('18');
  await expect(page.getByTestId(ids.capacityRoom)).toContainText('0 pts');
});

test('Capacity_WhenCapacityExceedsWhatIsCommittedAgainst_ShouldWarnOverButNotBlock', async ({
  mount,
  page,
}) => {
  await stubApi(page, { current: capacityOf('s24', [entry(0, 15), entry(1, 15)]), committed: 40 });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(page.getByTestId(ids.capacityRoom)).toContainText('10 pts over');
  await expect(page.getByTestId(componentTestIds.capacityBar)).toHaveAttribute('data-tone', 'over');
});

test('Capacity_WhenNothingEntered_ShouldPrefillTheDefaultVelocityAndPlanAgainstVelocity', async ({
  mount,
  page,
}) => {
  await stubApi(page, { committed: 12 });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(velocityBox(page)).toHaveValue('25');
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(0);
  await expect(page.getByTestId(ids.capacityEmpty)).toHaveText(
    'No capacity entered: planning uses velocity 20',
  );
  await expect(page.getByTestId(ids.capacityCallout)).toContainText('No capacity entered');
  await expect(page.getByTestId(ids.capacityRoom)).toContainText('8 pts');
  await expect(page.getByTestId(componentTestIds.capacityBar)).toHaveAttribute(
    'data-tone',
    'velocity',
  );
  await expect(save(page)).toBeDisabled();
});

test('Capacity_WhenAnOldPointsOnlySprintHasNoVelocity_ShouldLeaveTheVelocityBoxEmpty', async ({
  mount,
  page,
}) => {
  const stub = await stubApi(page, {
    current: capacityOf('s24', [entry(0, 6)], { velocity: null }),
  });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(velocityBox(page)).toHaveValue('');
  await days(page, 0).fill('8');
  await save(page).click();
  await expect(page.getByTestId(ids.capacityError)).toHaveText('Capacity needs a velocity.');
  expect(stub.puts).toEqual([]);
});

test('Capacity_WhenTypedVelocityFarFromTheHistory_ShouldWarnEitherWayButNotAtTheEdge', async ({
  mount,
  page,
}) => {
  await stubApi(page, { current: capacityOf('s24', [entry(0, 20)], { velocity: 20 }) });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  const callout = page.getByTestId(ids.capacityCallout);

  await expect(callout).toHaveAttribute('data-tone', 'info');
  await expect(callout).toContainText('In line with recent velocity');
  await velocityBox(page).fill('22');
  await expect(callout).toHaveAttribute('data-tone', 'info');

  await velocityBox(page).fill('23');
  await expect(callout).toHaveAttribute('data-tone', 'warn');
  await expect(callout).toContainText('Velocity 15% above recent sprints');
  await velocityBox(page).fill('15');
  await expect(callout).toHaveAttribute('data-tone', 'warn');
  await expect(callout).toContainText('Velocity 25% below recent sprints');
});

test('Capacity_WhenProjectHasAnOverride_ShouldCompareWithIt', async ({ mount, page }) => {
  await stubApi(page, {
    current: capacityOf('s24', [entry(0, 21)], { velocity: 21 }),
    velocity: { ...velocity, override: 30 },
  });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(page.getByTestId(ids.capacityKpiVelocity)).toHaveText('30');
  await expect(page.getByTestId(ids.capacityCallout)).toContainText(
    'Velocity 30% below the override',
  );
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

test('Capacity_WhenDaysAreTheDefault_ShouldShowItWithAMarkerAndResetAfterAnEdit', async ({
  mount,
  page,
}) => {
  await stubApi(page, {
    current: capacityOf('s24', [entry(0, 9), entry(1, 9, 6)], { defaultDays: 9, velocity: 18 }),
  });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(days(page, 0)).toHaveValue('9');
  await expect(page.getByTestId(ids.capacityDaysDefault)).toHaveCount(1);
  await expect(days(page, 1)).toHaveValue('6');
  await expect(page.getByTestId(ids.capacityDaysReset)).toHaveCount(1);
  await expect(days(page, 0)).toHaveAttribute('max', '10');
  await expect(days(page, 0)).toHaveAttribute('step', '0.5');

  await days(page, 0).fill('8');
  await expect(page.getByTestId(ids.capacityDaysDefault)).toHaveCount(0);
  await expect(page.getByTestId(ids.capacityDaysReset)).toHaveCount(2);
  await page.getByTestId(ids.capacityDaysReset).first().click();
  await expect(days(page, 0)).toHaveValue('9');
  await expect(page.getByTestId(ids.capacityDaysDefault)).toHaveCount(1);
});

test('Capacity_WhenSprintSwitchedAfterAnEdit_ShouldDropTheEditAndDisableSave', async ({
  mount,
  page,
}) => {
  const stub = await stubApi(page, { current: capacityOf('s24', [entry(0, 2)]) });
  const slot = await mount(<CapacitySlot project={project} sprint={sprint} sprints={sprints} />);
  await days(page, 0).fill('7');
  await expect(save(page)).toBeEnabled();

  await slot.update(<CapacitySlot project={project} sprint={sprints[2]!} sprints={sprints} />);

  await expect(days(page, 0)).toHaveValue('10');
  await expect(save(page)).toBeDisabled();
  expect(stub.puts).toEqual([]);
});

test('Capacity_WhenATeammateSavesWhileFormIsDirty_ShouldKeepTheirRowInTheSave', async ({
  mount,
  page,
}) => {
  const stub = await stubApi(page, { current: capacityOf('s24', [entry(0, 15), entry(2, 15)]) });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await days(page, 0).fill('3');

  stub.setCurrent(capacityOf('s24', [entry(0, 10), entry(1, 10, 8), entry(2, 10)]));
  await refetchOnFocus(page);

  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(3);
  await expect(days(page, 1)).toHaveValue('8');
  await expect(days(page, 0)).toHaveValue('3');
  await expect(page.getByTestId(ids.capacityConflict)).toHaveCount(0);
  await save(page).click();
  await expect.poll(() => stub.puts.length).toBe(1);
  expect(stub.puts[0]).toEqual({
    velocity: 30,
    members: [
      { userId: 'u1', days: 3, note: null },
      { userId: 'u2', days: 8, note: null },
      { userId: 'u3', days: null, note: null },
    ],
  });
});

test('Capacity_WhenATeammateChangesARowYouChanged_ShouldSayWhoAndKeepYours', async ({
  mount,
  page,
}) => {
  const stub = await stubApi(page, { current: capacityOf('s24', [entry(1, 5)]) });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await days(page, 0).fill('4');
  await expect(page.getByTestId(ids.capacityConflict)).toHaveCount(0);

  stub.setCurrent(capacityOf('s24', [entry(1, 8, 9)]));
  await refetchOnFocus(page);

  await expect(page.getByTestId(ids.capacityConflict)).toContainText('Tom changed capacity');
  await expect(days(page, 0)).toHaveValue('4');
  await save(page).click();
  await expect.poll(() => stub.puts.length).toBe(1);
  expect(stub.puts[0]).toEqual({
    velocity: 30,
    members: [{ userId: 'u2', days: 4, note: null }],
  });
});

test('Capacity_WhenAMemberIsAddedAndRemoved_ShouldChangeTheTeamSizeAndSaveTheSet', async ({
  mount,
  page,
}) => {
  const stub = await stubApi(page, { current: capacityOf('s24', [entry(0, 30)]) });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(rowPoints(page, 0)).toHaveText('30');
  const add = page.getByTestId(ids.capacityAdd);
  await expect(add.locator('option')).toHaveText(['+ Add member', 'Tom', 'Mei']);

  await add.selectOption({ label: 'Tom' });
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(2);
  await expect(rowPoints(page, 0)).toHaveText('15');
  await expect(rowPoints(page, 1)).toHaveText('15');
  await expect(add.locator('option')).toHaveText(['+ Add member', 'Mei']);

  await add.selectOption({ label: 'Mei' });
  await expect(rowPoints(page, 0)).toHaveText('10');
  await page.getByTestId(ids.capacityRemove).nth(1).click();
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(2);
  await expect(page.getByTestId(ids.capacityTotal)).toHaveText('30');

  await save(page).click();
  await expect.poll(() => stub.puts.length).toBe(1);
  expect(stub.puts[0]).toEqual({
    velocity: 30,
    members: [
      { userId: 'u1', days: null, note: null },
      { userId: 'u3', days: null, note: null },
    ],
  });
});

test('Capacity_WhenCopiedFromASprintWithAFormerMember_ShouldNotCountThemInTheTotalOrSaveThem', async ({
  mount,
  page,
}) => {
  const former = { user: user('u9', 'Former'), days: null, points: 9, note: null };
  const stub = await stubApi(page, {
    previous: capacityOf('s23', [entry(0, 6), entry(1, 5), entry(2, 4), former], { velocity: 30 }),
  });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await page.getByTestId(ids.capacityCopy).click();
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(3);
  await expect(page.getByTestId(ids.capacityTotal)).toHaveText('30');
  await expect(page.getByTestId(ids.capacityKpiCapacity)).toHaveText('30');
  await save(page).click();
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

test('Capacity_WhenCopied_ShouldFillDaysNotesAndVelocityFromThePreviousSprintThenSave', async ({
  mount,
  page,
}) => {
  const stub = await stubApi(page, {
    current: capacityOf('s24', [entry(0, 30)], { velocity: 12 }),
    previous: capacityOf('s23', [entry(0, 10, 6, 'Off Fri'), entry(1, 10), entry(2, 10, 12)], {
      velocity: 36,
    }),
  });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(velocityBox(page)).toHaveValue('12');
  await expect(save(page)).toBeDisabled();

  await page.getByTestId(ids.capacityCopy).click();

  await expect(page.getByTestId(ids.capacityCopy)).toHaveText('Copy from Sprint 23');
  await expect(velocityBox(page)).toHaveValue('36');
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(3);
  await expect(days(page, 0)).toHaveValue('6');
  await expect(days(page, 1)).toHaveValue('10');
  // A typed 12 days in a ten-day sprint comes down to the working days.
  await expect(days(page, 2)).toHaveValue('10');
  await expect(page.getByTestId(ids.capacityNote).first()).toHaveValue('Off Fri');
  await expect(page.getByTestId(ids.capacityTotal)).toHaveText('31.2');
  expect(stub.puts).toEqual([]);

  await save(page).click();
  await expect.poll(() => stub.puts.length).toBe(1);
  expect(stub.puts[0]).toEqual({
    velocity: 36,
    members: [
      { userId: 'u1', days: 6, note: 'Off Fri' },
      { userId: 'u2', days: null, note: null },
      { userId: 'u3', days: 10, note: null },
    ],
  });
  await expect(save(page)).toBeDisabled();
});

test('Capacity_WhenPreviousSprintHasNoEntries_ShouldDisableCopy', async ({ mount, page }) => {
  await stubApi(page);
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(page.getByTestId(ids.capacityCopy)).toBeDisabled();
});

test('Capacity_WhenFirstSprint_ShouldOfferNoNamedCopy', async ({ mount, page }) => {
  await stubApi(page);
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={[sprint]} />);
  await expect(page.getByTestId(ids.capacityCopy)).toHaveText('Copy from previous sprint');
  await expect(page.getByTestId(ids.capacityCopy)).toBeDisabled();
});

test('Capacity_WhenCleared_ShouldSaveAnEmptySetAndPlanAgainstVelocity', async ({ mount, page }) => {
  const stub = await stubApi(page, {
    current: capacityOf('s24', [entry(0, 15), entry(1, 15)]),
  });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(page.getByTestId(ids.capacityTotal)).toHaveText('30');

  await page.getByTestId(ids.capacityClear).click();
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(0);
  await expect(page.getByTestId(ids.capacityEmpty)).toContainText('planning uses velocity 20');
  await save(page).click();

  await expect.poll(() => stub.puts.length).toBe(1);
  expect(stub.puts[0]).toEqual({ velocity: 30, members: [] });
});

test('Capacity_WhenSavedWithANote_ShouldSendTheNoteAndNullDaysForTheDefault', async ({
  mount,
  page,
}) => {
  const stub = await stubApi(page, { current: capacityOf('s24', [entry(1, 30)]) });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await page.getByTestId(ids.capacityNote).fill('Away all sprint');
  await days(page, 0).fill('0');
  await save(page).click();
  await expect.poll(() => stub.puts.length).toBe(1);
  expect(stub.puts[0]).toEqual({
    velocity: 30,
    members: [{ userId: 'u2', days: 0, note: 'Away all sprint' }],
  });
});

test('Capacity_WhenDaysInvalid_ShouldNameTheMemberAndNotSave', async ({ mount, page }) => {
  const stub = await stubApi(page, { current: capacityOf('s24', [entry(1, 30)]) });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await days(page, 0).fill('10.5');
  await expect(rowPoints(page, 0)).toHaveText('—');
  await save(page).click();
  await expect(page.getByTestId(ids.capacityError)).toHaveText(
    'Tom: days are between 0 and 10, in steps of half a day.',
  );
  expect(stub.puts).toEqual([]);
  await days(page, 0).fill('3');
  await expect(page.getByTestId(ids.capacityError)).toHaveCount(0);
});

test('Capacity_WhenSaveRefused_ShouldShowTheServersMessageAndKeepTheForm', async ({
  mount,
  page,
}) => {
  await stubApi(page, { putStatus: 400, current: capacityOf('s24', [entry(0, 30)]) });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await days(page, 0).fill('5');
  await save(page).click();
  await expect(page.getByTestId(ids.capacityError)).toHaveText(
    'Capacity can only be entered for members of the project.',
  );
  await expect(days(page, 0)).toHaveValue('5');
  await expect(save(page)).toBeEnabled();
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

test('Capacity_WhenTheCountryHasHolidays_ShouldSayHowManyAndNameThem', async ({ mount, page }) => {
  await stubApi(page, {
    current: capacityOf('s24', [entry(0, 27)], {
      defaultDays: 9,
      holidayCountry: 'NZ',
      holidays: [{ date: '2026-10-26', name: 'Labour Day' }],
    }),
  });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(page.getByTestId(ids.capacityCalendar)).toHaveText(
    '10 working days · 1 public holiday (NZ)',
  );
  await expect(page.getByTestId(ids.capacityHolidays)).toHaveText('Labour Day (Mon 26 Oct)');
  await expect(days(page, 0)).toHaveValue('9');
  await expect(rowPoints(page, 0)).toHaveText('27');
  await expect(page.getByTestId(ids.capacityNoCountry)).toHaveCount(0);
});

test('Capacity_WhenProjectHasNoCountry_ShouldPointMembersAndLinkAdminsToSettings', async ({
  mount,
  page,
}) => {
  await stubApi(page);
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(page.getByTestId(ids.capacityNoCountry)).toContainText(
    'No public holidays: pick a country in Settings › General',
  );
  await expect(page.getByTestId(ids.capacityNoCountryLink)).toHaveCount(0);
});

test('Capacity_WhenAdminAndNoCountry_ShouldLinkToTheGeneralSettings', async ({ mount, page }) => {
  await stubApi(page);
  await mount(
    <CapacityPanel project={{ ...project, role: 'admin' }} sprint={sprint} sprints={sprints} />,
  );
  await expect(page.getByTestId(ids.capacityNoCountryLink)).toHaveAttribute(
    'href',
    '/p/SHIP/settings/general',
  );
});

test('Capacity_WhenTheSprintHasEnded_ShouldShowStoredDaysAndPointsWithNoInputs', async ({
  mount,
  page,
}) => {
  await stubApi(page, {
    current: capacityOf('s24', [entry(0, 12.5, 8, 'Off Fri'), entry(1, 17.5)], {
      ended: true,
      velocity: 30,
    }),
  });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  await expect(page.getByTestId(ids.capacityEnded)).toBeVisible();
  await expect(page.getByTestId(ids.capacityRow)).toHaveCount(2);
  await expect(page.locator('input, select')).toHaveCount(0);
  await expect(page.getByTestId(ids.capacitySave)).toHaveCount(0);
  await expect(velocityBox(page)).toHaveText('30');
  await expect(days(page, 0)).toHaveText('8');
  await expect(days(page, 1)).toHaveText('10');
  await expect(rowPoints(page, 0)).toHaveText('12.5');
  await expect(page.getByTestId(ids.capacityNote).first()).toHaveText('Off Fri');
  await expect(page.getByTestId(ids.capacityTotal)).toHaveText('30');
});

test('Capacity_WhenWide_ShouldKeepTheSummaryBesideTheTableAndTheTotalUnderItsColumn', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await stubApi(page, { current: capacityOf('s24', [entry(0, 15), entry(1, 15)]) });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  const box = async (locator: ReturnType<Page['locator']>) => (await locator.boundingBox())!;
  const table = await box(page.locator('table'));
  const summary = await box(page.getByRole('complementary'));
  expect(summary.x).toBeGreaterThanOrEqual(table.x + table.width);
  // Aside and table are as tall as their own content: nothing stretches the shorter one.
  expect(summary.height).toBeLessThan(table.height + 150);
  const heading = await box(page.getByRole('columnheader', { name: 'Capacity' }));
  const total = await box(page.getByTestId(ids.capacityTotal));
  const figure = await box(rowPoints(page, 0));
  expect(Math.abs(heading.x + heading.width - 12 - (total.x + total.width + 24))).toBeLessThan(40);
  expect(Math.abs(figure.x - total.x)).toBeLessThan(4);
});

test('Capacity_WhenNarrow_ShouldStackTheSummaryBelowAndNeverScrollThePage', async ({
  mount,
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 800 });
  await stubApi(page, { current: capacityOf('s24', [entry(0, 15), entry(1, 15)]) });
  await mount(<CapacityPanel project={project} sprint={sprint} sprints={sprints} />);
  const table = (await page.locator('table').boundingBox())!;
  const summary = (await page.getByRole('complementary').boundingBox())!;
  expect(summary.y).toBeGreaterThanOrEqual(table.y + table.height);
  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  );
  expect(overflow).toBeLessThanOrEqual(0);
});
