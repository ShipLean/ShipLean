import { describe, expect, test } from 'vitest';
import {
  blocker,
  emptyForm,
  keyOf,
  suggestKey,
  toCreate,
  withSprintDefaults,
  type NewProjectForm,
} from './model';

const ready: NewProjectForm = {
  ...emptyForm,
  name: 'Customer Support Hub',
  sprint: true,
  start: '2026-10-26',
  end: '2026-11-06',
};

describe('suggestKey', () => {
  test.each([
    ['Customer Support Hub', 'CSH'],
    ['Payments', 'PAYM'],
    ['Very Long Name Indeed Here', 'VLNI'],
    ['  ', ''],
    ['2026', ''],
  ])('suggestKey_When%s_ShouldBe%s', (name, key) => {
    expect(suggestKey(name)).toBe(key);
  });
});

describe('keyOf', () => {
  test('keyOf_WhenEdited_ShouldKeepWhatWasTyped', () => {
    expect(keyOf({ ...ready, keyEdited: true, typedKey: 'ZZ' })).toBe('ZZ');
  });
  test('keyOf_WhenNotEdited_ShouldFollowTheName', () => {
    expect(keyOf({ ...ready, typedKey: 'ZZ' })).toBe('CSH');
  });
});

describe('blocker', () => {
  test('blocker_WhenReady_ShouldBeNull', () => {
    expect(blocker(ready, true, 'Sprint 1')).toBeNull();
  });
  test('blocker_WhenNameBlank_ShouldAskForName', () => {
    expect(blocker({ ...ready, name: ' ' }, true, 'Sprint 1')).toBe('Give the project a name');
  });
  test('blocker_WhenKeyUnavailable_ShouldAskToFixKey', () => {
    expect(blocker(ready, false, 'Sprint 1')).toBe('Fix the key to continue');
  });
  test('blocker_WhenNoWorkingDay_ShouldAsk', () => {
    expect(blocker({ ...ready, workingDays: [] }, true, 'Sprint 1')).toBe(
      'Pick at least one working day',
    );
  });
  test('blocker_WhenSprintWithoutDates_ShouldAsk', () => {
    expect(blocker({ ...ready, end: '' }, true, 'Sprint 1')).toBe('Pick the dates of Sprint 1');
  });
  test('blocker_WhenSprintEndsBeforeStart_ShouldRefuse', () => {
    expect(blocker({ ...ready, end: '2026-10-01' }, true, 'Sprint 1')).toBe(
      'Sprint 1 cannot end before it starts',
    );
  });
  test('blocker_WhenNoSprintAndNoDates_ShouldBeNull', () => {
    expect(blocker({ ...ready, sprint: false, start: '', end: '' }, true, 'Sprint 1')).toBeNull();
  });
  test('blocker_WhenCopyWithoutSource_ShouldAsk', () => {
    expect(blocker({ ...ready, from: 'copy' }, true, 'Sprint 1')).toBe('Pick a project to copy');
  });
});

describe('toCreate', () => {
  test('toCreate_WhenDefaults_ShouldCarryNoSourceAndSprintOne', () => {
    expect(toCreate(ready, 'Sprint 1')).toEqual({
      name: 'Customer Support Hub',
      key: 'CSH',
      copyFromProjectId: null,
      memberEmails: [],
      workingDays: [1, 2, 3, 4, 5],
      firstSprint: { name: 'Sprint 1', startDate: '2026-10-26', endDate: '2026-11-06', goal: '' },
    });
  });
  test('toCreate_WhenCopyAndNoSprint_ShouldCarrySourceAndNoSprint', () => {
    const create = toCreate(
      { ...ready, from: 'copy', copyFromId: 'p-1', sprint: false },
      'Sprint 1',
    );
    expect(create.copyFromProjectId).toBe('p-1');
    expect(create.firstSprint).toBeNull();
  });
  test('toCreate_WhenSwitchedBackToDefaults_ShouldDropTheSource', () => {
    expect(
      toCreate({ ...ready, from: 'defaults', copyFromId: 'p-1' }, 'Sprint 1').copyFromProjectId,
    ).toBeNull();
  });
});

describe('withSprintDefaults', () => {
  const defaults = {
    name: 'Sprint 1',
    startDate: '2026-10-26',
    endDate: '2026-11-08',
    goal: '',
  };
  test('withSprintDefaults_WhenDatesNotEdited_ShouldTakeTheServersDates', () => {
    const form = withSprintDefaults({ ...emptyForm, start: '', end: '' }, defaults);
    expect([form.start, form.end]).toEqual(['2026-10-26', '2026-11-08']);
  });
  test('withSprintDefaults_WhenDatesEdited_ShouldKeepWhatWasTyped', () => {
    const typed = { ...emptyForm, datesEdited: true, start: '2026-12-01', end: '2026-12-14' };
    expect(withSprintDefaults(typed, defaults)).toBe(typed);
  });
  test('withSprintDefaults_WhenDefaultsNotLoaded_ShouldLeaveDatesEmpty', () => {
    expect(withSprintDefaults(emptyForm, undefined).start).toBe('');
  });
  test('blocker_WhenSprintNameNotLoaded_ShouldAskForDates', () => {
    expect(blocker({ ...emptyForm, name: 'X', sprint: true }, true, undefined)).toBe(
      'Pick the dates of Sprint 1',
    );
  });
});
