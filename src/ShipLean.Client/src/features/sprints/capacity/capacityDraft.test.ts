import { expect, test } from 'vitest';
import type { MemberDto } from '../../../api/projects';
import type { UserRef } from '../../../api/refs';
import { draftOf, draftTotal, toPut } from './capacityDraft';

const EMPTY_EXTRAS = {
  velocity: null,
  velocityDefault: 0,
  workingDays: 10,
  defaultDays: 10,
  holidays: [],
  holidayCountry: null,
  ended: false,
};
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

test('draftOf_WhenCapacityEntered_ShouldFillThoseMembersOnly', () => {
  const draft = draftOf({
    sprintId: 's',
    teamTotal: 5,
    ...EMPTY_EXTRAS,
    members: [{ user: user('a', 'Ana'), days: null, points: 5, note: 'Off Fri' }],
  });
  expect(draft).toEqual({ a: { points: '5', note: 'Off Fri' } });
});

test('draftTotal_WhenSomeBlank_ShouldSumTheEnteredAndSayEntered', () => {
  expect(
    draftTotal(members, { a: { points: '3', note: '' }, b: { points: '', note: '' } }),
  ).toEqual({
    total: 3,
    entered: true,
  });
  expect(draftTotal(members, {})).toEqual({ total: 0, entered: false });
});

test('draftTotal_WhenZeroEntered_ShouldCountAsEntered', () => {
  expect(draftTotal(members, { a: { points: '0', note: '' } })).toEqual({
    total: 0,
    entered: true,
  });
});

test('toPut_WhenBlankRows_ShouldLeaveThemOutAndTrimNotes', () => {
  expect(toPut(members, { a: { points: '2.5', note: '  away ' } })).toEqual({
    put: [{ userId: 'a', days: 2.5, note: 'away' }],
  });
});

test('toPut_WhenNegativeTooPreciseOrNotANumber_ShouldNameTheMember', () => {
  for (const points of ['-1', '1.234', 'abc', '10000']) {
    const result = toPut(members, { b: { points, note: '' } });
    expect(result).toHaveProperty('error');
    expect((result as { error: string }).error).toContain('Bo');
  }
});

test('draftTotal_WhenARowBelongsToSomeoneWhoLeft_ShouldLeaveItOutLikeThePut', () => {
  const draft = { a: { points: '3', note: '' }, gone: { points: '9', note: '' } };
  expect(draftTotal(members, draft)).toEqual({ total: 3, entered: true });
  expect(toPut(members, draft)).toEqual({ put: [{ userId: 'a', days: 3, note: null }] });
  expect(draftTotal(members, { gone: { points: '9', note: '' } })).toEqual({
    total: 0,
    entered: false,
  });
});
