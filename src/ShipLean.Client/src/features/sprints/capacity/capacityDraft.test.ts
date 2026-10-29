import { expect, test } from 'vitest';
import type { MemberDto } from '../../../api/projects';
import type { UserRef } from '../../../api/refs';
import type { SprintCapacityDto } from '../../../api/sprints';
import {
  draftOf,
  draftTotals,
  effectiveDaysOf,
  memberPoints,
  parseDays,
  parseVelocity,
  tableMembers,
  toPut,
  velocityTextOf,
  type CapacityShape,
  type DraftRow,
} from './capacityDraft';

const shape: CapacityShape = { workingDays: 10, defaultDays: 10 };
const user = (id: string, name: string): UserRef => ({
  id,
  name,
  initials: name.slice(0, 2),
  avatarColour: '#112233',
  photoUrl: null,
});
const member = (id: string, name: string) =>
  ({ user: user(id, name), email: '', role: 'member', status: 'active' }) as MemberDto;
const members = [member('a', 'Ana'), member('b', 'Bo')];
const row = (days = '', note = '', present = true): DraftRow => ({ present, days, note });
const capacityOf = (overrides: Partial<SprintCapacityDto>): SprintCapacityDto => ({
  sprintId: 's',
  members: [],
  teamTotal: 0,
  velocity: null,
  velocityDefault: 0,
  workingDays: 10,
  defaultDays: 10,
  holidays: [],
  holidayCountry: null,
  ended: false,
  ...overrides,
});

// The server's CapacityPoints cases (ShipLean.UnitTest/Core/CapacityPointsTests.cs): the client must give the same numbers.
test('memberPoints_WhenAllDaysAndAnyTeamSize_ShouldBeAnEqualShareOfVelocity', () => {
  for (const n of [1, 2, 3, 4, 5]) {
    expect(memberPoints(30, n, 10, shape)).toBe(Math.round((3000 / n) * 1) / 100);
  }
});

test('memberPoints_WhenDaysAreZeroHalfOrAHalfDayShort_ShouldScaleTheShare', () => {
  expect(memberPoints(30, 3, 0, shape)).toBe(0);
  expect(memberPoints(40, 2, 5, shape)).toBe(10);
  expect(memberPoints(20, 1, 9.5, shape)).toBe(19);
});

test('memberPoints_WhenNoWorkingDaysOrNoMembers_ShouldBeZero', () => {
  expect(memberPoints(30, 2, 0, { workingDays: 0, defaultDays: 0 })).toBe(0);
  expect(memberPoints(30, 0, 5, shape)).toBe(0);
});

test('memberPoints_WhenResultNeedsRounding_ShouldRoundHalfAwayFromZeroToTwoDecimals', () => {
  expect(memberPoints(10, 3, 10, shape)).toBe(3.33);
  expect(memberPoints(0.05, 1, 5, shape)).toBe(0.03);
  expect(memberPoints(1, 8, 10, shape)).toBe(0.13);
});

test('memberPoints_WhenResultIsAHalfCent_ShouldRoundUpLikeTheServersDecimal', () => {
  const sixDays = { workingDays: 6, defaultDays: 6 };
  expect(memberPoints(5.1, 1, 0.5, sixDays)).toBe(0.43);
  expect(memberPoints(0.3, 1, 1.5, sixDays)).toBe(0.08);
});

test('memberPoints_WhenOverTheLimit_ShouldCapAtLargest', () => {
  expect(memberPoints(9999.99, 1, 20, shape)).toBe(9999.99);
});

test('memberPoints_WhenOneHolidayInTenDays_ShouldBeNinetyPercentOfTheShare', () => {
  expect(memberPoints(40, 4, null, { workingDays: 10, defaultDays: 9 })).toBe(9);
});

test('memberPoints_WhenTypedDaysExceedTheWorkingDays_ShouldCountOnlyTheWorkingDays', () => {
  expect(memberPoints(40, 4, 14, shape)).toBe(10);
});

test('draftOf_WhenCapacityEntered_ShouldMakeThoseMembersPresentWithTheirTypedDays', () => {
  const draft = draftOf(
    capacityOf({
      members: [
        { user: user('a', 'Ana'), days: null, points: 5, note: 'Off Fri' },
        { user: user('b', 'Bo'), days: 7.5, points: 4, note: null },
      ],
    }),
  );
  expect(draft).toEqual({
    a: { present: true, days: '', note: 'Off Fri' },
    b: { present: true, days: '7.5', note: '' },
  });
});

test('draftOf_WhenCopiedIntoAShorterSprint_ShouldBringTypedDaysDownToItsWorkingDays', () => {
  const draft = draftOf(
    capacityOf({ members: [{ user: user('b', 'Bo'), days: 9.5, points: 4, note: null }] }),
    8,
  );
  expect(draft).toEqual({ b: { present: true, days: '8', note: '' } });
});

test('velocityTextOf_WhenNoVelocityStored_ShouldDefaultOnlyWhenTheTableIsEmpty', () => {
  expect(velocityTextOf(capacityOf({ velocity: 30, velocityDefault: 25 }))).toBe('30');
  expect(velocityTextOf(capacityOf({ velocity: null, velocityDefault: 25.5 }))).toBe('25.5');
  const oldPoints = capacityOf({
    velocity: null,
    velocityDefault: 25,
    members: [{ user: user('a', 'Ana'), days: null, points: 5, note: null }],
  });
  expect(velocityTextOf(oldPoints)).toBe('');
});

test('parseVelocity_WhenNotAValidVelocity_ShouldBeUndefined', () => {
  expect(parseVelocity('30')).toBe(30);
  expect(parseVelocity('0')).toBe(0);
  expect(parseVelocity('9999.99')).toBe(9999.99);
  for (const text of ['', ' ', 'abc', '-1', '1.234', '10000']) {
    expect(parseVelocity(text)).toBeUndefined();
  }
});

test('parseDays_WhenEmptyTypedOrInvalid_ShouldSayDefaultTheNumberOrNaN', () => {
  expect(parseDays('', 10)).toBeNull();
  expect(parseDays('7.5', 10)).toBe(7.5);
  expect(parseDays('10', 10)).toBe(10);
  for (const text of ['-1', '0.3', '10.5', 'abc']) {
    expect(parseDays(text, 10)).toBeNaN();
  }
});

test('effectiveDaysOf_WhenEmpty_ShouldBeTheDefaultAndNeverMoreThanTheWorkingDays', () => {
  expect(effectiveDaysOf(row(''), { workingDays: 10, defaultDays: 9 })).toBe(9);
  expect(effectiveDaysOf(row('4'), shape)).toBe(4);
  expect(effectiveDaysOf(row('x'), shape)).toBeUndefined();
});

test('draftTotals_WhenRowsInTheTable_ShouldSumTheRoundedShares', () => {
  const draft = { a: row(), b: row('5') };
  const result = draftTotals(members, draft, 30, shape);
  expect(result.points).toEqual({ a: 15, b: 7.5 });
  expect(result).toMatchObject({ total: 22.5, entered: true });
});

test('draftTotals_WhenTeamSizeChanges_ShouldShareVelocityAgain', () => {
  const draft = { a: row(), b: row('', '', false) };
  expect(draftTotals(members, draft, 30, shape)).toMatchObject({ total: 30, entered: true });
});

test('draftTotals_WhenTheTableIsEmpty_ShouldNotBeEntered', () => {
  expect(draftTotals(members, {}, 30, shape)).toEqual({ total: 0, entered: false, points: {} });
});

test('draftTotals_WhenVelocityOrDaysCannotBeRead_ShouldLeaveThatShareOut', () => {
  const draft = { a: row('abc'), b: row() };
  expect(draftTotals(members, draft, 30, shape).points).toEqual({ a: undefined, b: 15 });
  expect(draftTotals(members, draft, undefined, shape)).toMatchObject({ total: 0, entered: true });
});

test('draftTotals_WhenARowBelongsToSomeoneWhoLeft_ShouldLeaveItOut', () => {
  const draft = { a: row(), gone: row() };
  expect(tableMembers(members, draft).map((m) => m.user.id)).toEqual(['a']);
  expect(draftTotals(members, draft, 30, shape)).toMatchObject({ total: 30, entered: true });
  expect(toPut(members, draft, '30', shape)).toEqual({
    put: { velocity: 30, members: [{ userId: 'a', days: null, note: null }] },
  });
});

test('toPut_WhenRowsAndAVelocity_ShouldSendDaysNullForTheDefaultAndTrimNotes', () => {
  expect(toPut(members, { a: row('', '  away '), b: row('4.5') }, '30', shape)).toEqual({
    put: {
      velocity: 30,
      members: [
        { userId: 'a', days: null, note: 'away' },
        { userId: 'b', days: 4.5, note: null },
      ],
    },
  });
});

test('toPut_WhenRowsButNoVelocity_ShouldAskForOne', () => {
  expect(toPut(members, { a: row() }, '', shape)).toEqual({ error: 'Capacity needs a velocity.' });
});

test('toPut_WhenVelocityInvalid_ShouldSayWhatIsAllowed', () => {
  for (const text of ['-1', '1.234', 'abc', '10000']) {
    const result = toPut(members, { a: row() }, text, shape);
    expect((result as { error: string }).error).toContain('Velocity is zero or more points');
  }
});

test('toPut_WhenDaysNegativeHalfwayOrTooMany_ShouldNameTheMember', () => {
  for (const days of ['-1', '0.3', '11', 'abc']) {
    const result = toPut(members, { b: row(days) }, '30', shape);
    expect((result as { error: string }).error).toBe(
      'Bo: days are between 0 and 10, in steps of half a day.',
    );
  }
});

test('toPut_WhenTheTableIsEmpty_ShouldSendNoMembersAndLeaveVelocityWhenBlank', () => {
  expect(toPut(members, {}, '', shape)).toEqual({ put: { velocity: null, members: [] } });
  expect(toPut(members, { a: row('', '', false) }, '28', shape)).toEqual({
    put: { velocity: 28, members: [] },
  });
});
