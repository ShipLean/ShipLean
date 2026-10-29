import { describe, expect, it } from 'vitest';
import type { ProjectSettingsDto, StateDto } from '../../../api/projects';
import { WORK_ITEM_TYPE_IDS } from '../../../api/refs';
import { changesBetween, parse, rebase, toDraft, toPut, validate } from './model';

const user = { id: 'u1', name: 'Joel', initials: 'J', avatarColour: '#5b6b8c', photoUrl: null };
const statesOf = (): StateDto[] =>
  WORK_ITEM_TYPE_IDS.flatMap((type) => [
    {
      id: `${type}-new`,
      type,
      name: 'New',
      category: 'proposed' as const,
      order: 1,
      itemCount: 0,
    },
    {
      id: `${type}-done`,
      type,
      name: 'Done',
      category: 'completed' as const,
      order: 2,
      itemCount: 2,
    },
  ]);
const saved: ProjectSettingsDto = {
  general: {
    key: 'SHIP',
    name: 'Storefront',
    description: 'd',
    workingDays: [5, 1, 2],
    agentConnectionId: null,
    repoUrl: null,
    repoBaseBranch: null,
    holidayCountry: null,
    agentConnections: [],
  },
  members: [{ user, email: 'joel@x.test', role: 'admin', status: 'active' }],
  sprints: { velocityWindow: 3, velocityOverride: null, sprintNaming: 'number' },
  states: statesOf(),
};

describe('settings draft model', () => {
  it('toDraft_WhenLoaded_ShouldSortDaysAndKeepOverrideBlank', () => {
    const draft = toDraft(saved);
    expect(draft.workingDays).toEqual([1, 2, 5]);
    expect(draft.velocityOverride).toBe('');
    expect(changesBetween(draft, draft, saved.states)).toEqual([]);
  });

  it('toDraft_WhenSavedWithoutNaming_ShouldFallBackToNumber', () => {
    const sprints = { velocityWindow: 3, velocityOverride: null };
    expect(toDraft({ ...saved, sprints }).sprintNaming).toBe('number');
  });

  it('sprintNaming_WhenChanged_ShouldBeAChangeAPutAndSurviveARebase', () => {
    const base = toDraft(saved);
    const draft = { ...base, sprintNaming: 'yearQuarterNumber' as const };
    expect(changesBetween(base, draft, saved.states)).toEqual([
      { tab: 'sprints', label: 'Sprint naming' },
    ]);
    const result = parse(draft, saved.states);
    if ('errors' in result) throw new Error('draft should parse');
    expect(toPut(draft, result.parsed).sprints.sprintNaming).toBe('yearQuarterNumber');
    const latest = { ...saved, general: { ...saved.general, name: 'Theirs' } };
    expect(rebase(latest, base, draft, saved.states).draft.sprintNaming).toBe('yearQuarterNumber');
    const theirs = {
      ...saved,
      sprints: { ...saved.sprints, sprintNaming: 'yearQuarterNumber' as const },
    };
    expect(rebase(theirs, base, base, saved.states).draft.sprintNaming).toBe('yearQuarterNumber');
  });

  it('holidayCountry_WhenPickedOrCleared_ShouldBeAChangeAndAPutOfCodeOrNull', () => {
    const base = toDraft(saved);
    expect(base.holidayCountry).toBe('');
    const draft = { ...base, holidayCountry: 'NZ' };
    expect(changesBetween(base, draft, saved.states)).toEqual([
      { tab: 'general', label: 'Public holidays' },
    ]);
    const result = parse(draft, saved.states);
    if ('errors' in result) throw new Error('draft should parse');
    expect(toPut(draft, result.parsed).general.holidayCountry).toBe('NZ');
    expect(toPut(base, result.parsed).general.holidayCountry).toBeNull();
    const latest = { ...saved, general: { ...saved.general, name: 'Theirs' } };
    expect(rebase(latest, base, draft, saved.states).draft.holidayCountry).toBe('NZ');
    const theirs = { ...saved, general: { ...saved.general, holidayCountry: 'AU' } };
    expect(rebase(theirs, base, base, saved.states).draft.holidayCountry).toBe('AU');
  });

  it('validate_WhenNothingWrong_ShouldFindNothing', () => {
    expect(validate(toDraft(saved), saved.states)).toEqual([]);
  });

  it('validate_WhenNoWorkingDayAndBlankName_ShouldNameTabAndField', () => {
    const draft = { ...toDraft(saved), name: ' ', workingDays: [] };
    expect(validate(draft, saved.states).map((e) => [e.tab, e.field])).toEqual([
      ['general', 'name'],
      ['general', 'workingDays'],
    ]);
  });

  it('validate_WhenForecastIsBad_ShouldPointAtSprints', () => {
    const draft = { ...toDraft(saved), velocityWindow: '0', velocityOverride: '1.234' };
    expect(validate(draft, saved.states).map((e) => [e.tab, e.field])).toEqual([
      ['sprints', 'velocityWindow'],
      ['sprints', 'velocityOverride'],
    ]);
  });

  it('toPut_WhenMemberRemovedAndOverrideSet_ShouldLeaveMemberOutAndParseOverride', () => {
    const base = toDraft(saved);
    const draft = {
      ...base,
      members: base.members.map((m) => ({ ...m, removed: true })),
      velocityOverride: '12.5',
    };
    const result = parse(draft, saved.states);
    if ('errors' in result) throw new Error('draft should parse');
    const put = toPut(draft, result.parsed);
    expect(put.members).toEqual([]);
    expect(put.sprints).toEqual({
      velocityWindow: 3,
      velocityOverride: 12.5,
      sprintNaming: 'number',
    });
  });

  it('changesBetween_WhenEveryTabEdited_ShouldListOneChangePerEdit', () => {
    const base = toDraft(saved);
    const draft = {
      ...base,
      name: 'Shop',
      members: [
        { ...base.members[0], role: 'member' as const },
        { email: 'new@x.test', user, role: 'member' as const, removed: false, fresh: true },
      ],
      velocityWindow: '4',
    };
    expect(changesBetween(base, draft, saved.states).map((c) => c.tab)).toEqual([
      'general',
      'members',
      'members',
      'sprints',
    ]);
  });

  it('changesBetween_WhenStatesStaged_ShouldListOneChangePerStateChange', () => {
    const base = toDraft(saved);
    const draft = {
      ...base,
      states: {
        add: [
          {
            id: 's1',
            type: 'story' as const,
            name: 'Review',
            category: 'inProgress' as const,
          },
        ],
        update: [],
        reorder: [{ type: 'story' as const, stateIds: ['s1'] }],
        remove: [],
      },
    };
    expect(changesBetween(base, draft, saved.states).map((c) => c.tab)).toEqual([
      'states',
      'states',
    ]);
  });

  it('rebase_WhenTeammateSaved_ShouldKeepTheirChangesAndReplayMine', () => {
    const base = toDraft(saved);
    const lee = { ...user, id: 'u2', name: 'Lee' };
    const draft = {
      ...base,
      name: 'Mine',
      members: [{ ...base.members[0], role: 'member' as const }],
    };
    const latest: ProjectSettingsDto = {
      ...saved,
      general: { ...saved.general, workingDays: [1, 2, 5, 6] },
      members: [
        ...saved.members,
        { user: lee, email: 'lee@x.test', role: 'member', status: 'active' },
      ],
    };
    const next = rebase(latest, base, draft, saved.states);
    expect(next.base).toEqual(toDraft(latest));
    expect(next.draft.name).toBe('Mine');
    expect(next.draft.workingDays).toEqual([1, 2, 5, 6]);
    expect(next.draft.members.map((m) => [m.user.id, m.role])).toEqual([
      ['u1', 'member'],
      ['u2', 'member'],
    ]);
  });

  it('validate_WhenTypeLosesItsCompletedState_ShouldPointAtTheTypeAndBlockParse', () => {
    const base = toDraft(saved);
    const draft = {
      ...base,
      states: {
        add: [],
        update: [{ stateId: 'bug-done', name: 'Done', category: 'resolved' as const }],
        reorder: [],
        remove: [],
      },
    };
    const errors = validate(draft, saved.states);
    expect(errors).toMatchObject([{ tab: 'states', field: 'stateType-bug', type: 'bug' }]);
    expect('errors' in parse(draft, saved.states)).toBe(true);
  });

  it('validate_WhenStateNameIsBlank_ShouldPointAtTheRow', () => {
    const base = toDraft(saved);
    const draft = {
      ...base,
      states: {
        add: [],
        update: [{ stateId: 'task-new', name: ' ', category: 'proposed' as const }],
        reorder: [],
        remove: [],
      },
    };
    expect(validate(draft, saved.states)).toMatchObject([
      { tab: 'states', field: 'state-task-new', type: 'task' },
    ]);
  });

  it('changesBetween_WhenStateDeleted_ShouldSayWhatMovesWhere', () => {
    const base = toDraft(saved);
    const draft = {
      ...base,
      states: {
        add: [],
        update: [],
        reorder: [],
        remove: [{ stateId: 'bug-done', targetStateId: 'bug-new' }],
      },
    };
    expect(changesBetween(base, draft, saved.states).map((c) => c.label)).toEqual([
      'Bug: deleted Done, 2 items move to New',
    ]);
  });

  it('rebase_WhenTeammateDeletedAStateIEdited_ShouldDropMyEditsToIt', () => {
    const base = toDraft(saved);
    const draft = {
      ...base,
      states: {
        add: [],
        update: [{ stateId: 'bug-new', name: 'Triage', category: 'proposed' as const }],
        reorder: [],
        remove: [],
      },
    };
    const latest = { ...saved, states: saved.states.filter((s) => s.id !== 'bug-new') };
    expect(rebase(latest, base, draft, saved.states).draft.states.update).toEqual([]);
  });
});
