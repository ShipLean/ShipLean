import { describe, expect, it } from 'vitest';
import type { StateChanges, StateDto } from '../../../api/projects';
import {
  addState,
  defaultTarget,
  deleteBlock,
  editState,
  itemsIn,
  moveState,
  rebaseStates,
  removeLabel,
  removeState,
  rowsOf,
  typeProblems,
} from './stateModel';

const none = (): StateChanges => ({ add: [], update: [], reorder: [], remove: [] });
const st = (
  id: string,
  name: string,
  category: StateDto['category'],
  order: number,
  itemCount = 0,
): StateDto => ({ id, type: 'story', name, category, order, itemCount });
const saved = [
  st('a', 'New', 'proposed', 1, 2),
  st('b', 'Active', 'inProgress', 2, 3),
  st('c', 'Closed', 'completed', 3, 4),
];

describe('stateModel', () => {
  it('rowsOf_WhenRenamed_ShouldKeepCategory', () => {
    const changes = editState(none(), saved, 'b', { name: 'Doing' });
    const row = rowsOf(saved, changes, 'story').find((r) => r.id === 'b');
    expect(row).toMatchObject({ name: 'Doing', category: 'inProgress' });
    expect(changes.update).toEqual([{ stateId: 'b', name: 'Doing', category: 'inProgress' }]);
  });

  it('editState_WhenPutBackAsSaved_ShouldDropTheChange', () => {
    const renamed = editState(none(), saved, 'b', { name: 'Doing' });
    expect(editState(renamed, saved, 'b', { name: 'Active' }).update).toEqual([]);
  });

  it('moveState_WhenMovedDown_ShouldStageOrder', () => {
    const changes = moveState(none(), saved, 'story', 'a', 1);
    expect(rowsOf(saved, changes, 'story').map((r) => r.id)).toEqual(['b', 'a', 'c']);
    expect(changes.reorder).toEqual([{ type: 'story', stateIds: ['b', 'a', 'c'] }]);
    expect(moveState(changes, saved, 'story', 'a', -1).reorder).toEqual([]);
  });

  it('removeState_WhenAddedInDraft_ShouldJustDisappear', () => {
    const added = addState(none(), saved, {
      id: 'n',
      type: 'story',
      name: 'Review',
      category: 'inProgress',
    });
    expect(removeState(added, 'n', 'a')).toEqual(none());
  });

  it('removeState_WhenSavedState_ShouldStageMoveAndCountIncoming', () => {
    const changes = removeState(none(), 'b', 'a');
    expect(changes.remove).toEqual([{ stateId: 'b', targetStateId: 'a' }]);
    const target = rowsOf(saved, changes, 'story').find((r) => r.id === 'a');
    expect(target && itemsIn(target)).toBe(5);
  });

  it('removeState_WhenTargetIsDeletedLater_ShouldRetargetEarlierDeletes', () => {
    const first = removeState(none(), 'b', 'a');
    const second = removeState(first, 'a', 'c');
    expect(second.remove).toEqual([
      { stateId: 'b', targetStateId: 'c' },
      { stateId: 'a', targetStateId: 'c' },
    ]);
  });

  it('deleteBlock_WhenLastOfRequiredCategory_ShouldSayWhy', () => {
    const rows = rowsOf(saved, none(), 'story');
    expect(deleteBlock(rows, 'c')).toContain('Completed');
    expect(deleteBlock(rows, 'a')).toContain('Proposed');
    expect(deleteBlock(rows, 'b')).toBeUndefined();
  });

  it('typeProblems_WhenCompletedRecategorised_ShouldNeedCompleted', () => {
    const changes = editState(none(), saved, 'c', { category: 'resolved' });
    const { typeError } = typeProblems(rowsOf(saved, changes, 'story'));
    expect(typeError).toContain('Completed');
  });

  it('typeProblems_WhenNameRepeated_ShouldFlagTheRow', () => {
    const changes = editState(none(), saved, 'b', { name: 'new' });
    const { rowErrors } = typeProblems(rowsOf(saved, changes, 'story'));
    expect([...rowErrors.keys()].sort()).toEqual(['a', 'b']);
  });

  it('addState_WhenATypeEndsWithRemoved_ShouldPutTheNewStateBeforeIt', () => {
    const withRemoved = [...saved, st('r', 'Removed', 'removed', 4)];
    const changes = addState(none(), withRemoved, {
      id: 'n',
      type: 'story',
      name: 'Review',
      category: 'inProgress',
    });
    expect(rowsOf(withRemoved, changes, 'story').map((r) => r.id)).toEqual([
      'a',
      'b',
      'c',
      'n',
      'r',
    ]);
    expect(changes.reorder).toEqual([{ type: 'story', stateIds: ['a', 'b', 'c', 'n', 'r'] }]);
    expect(
      addState(none(), saved, { id: 'n', type: 'story', name: 'x', category: 'inProgress' })
        .reorder,
    ).toEqual([]);
  });

  it('removeState_WhenItsTargetWasAddedInTheDraftAndThenDeleted_ShouldRetargetTheEarlierDelete', () => {
    const added = addState(none(), saved, {
      id: 'n',
      type: 'story',
      name: 'Doing',
      category: 'inProgress',
    });
    const moved = removeState(added, 'b', 'n');
    expect(moved.remove).toEqual([{ stateId: 'b', targetStateId: 'n' }]);
    const target = rowsOf(saved, moved, 'story').find((r) => r.id === 'n');
    expect(target && itemsIn(target)).toBe(3);
    const again = removeState(moved, 'n', 'a');
    expect(again.add).toEqual([]);
    expect(again.remove).toEqual([{ stateId: 'b', targetStateId: 'a' }]);
  });

  it('defaultTarget_WhenDeleting_ShouldPreferSameCategoryThenProposed', () => {
    const rows = rowsOf([...saved, st('d', 'Doing', 'inProgress', 4)], none(), 'story');
    expect(defaultTarget(rows, 'b')).toBe('d');
    expect(defaultTarget(rows, 'd')).toBe('b');
    expect(
      defaultTarget(
        rows.filter((r) => r.id !== 'd'),
        'b',
      ),
    ).toBe('a');
    expect(
      defaultTarget(
        rows.filter((r) => r.id === 'b'),
        'b',
      ),
    ).toBeUndefined();
  });

  it('removeLabel_WhenItemsMove_ShouldNameTypeStateCountAndTarget', () => {
    const changes = removeState(none(), 'b', 'a');
    expect(removeLabel(saved, changes, 'b')).toBe(
      'User Story: deleted Active, 3 items move to New',
    );
    expect(removeLabel(saved, removeState(none(), 'a', 'b'), 'a')).toBe(
      'User Story: deleted New, 2 items move to Active',
    );
  });

  describe('rebaseStates', () => {
    const base = saved;

    it('rebaseStates_WhenTeammateDeletedAStateIEdited_ShouldDropMyEditReorderAndDelete', () => {
      const mine: StateChanges = {
        add: [],
        update: [{ stateId: 'b', name: 'Doing', category: 'inProgress' }],
        reorder: [{ type: 'story', stateIds: ['b', 'a', 'c'] }],
        remove: [{ stateId: 'b', targetStateId: 'a' }],
      };
      const latest = saved.filter((s) => s.id !== 'b');
      expect(rebaseStates(latest, base, mine)).toEqual({
        add: [],
        update: [],
        reorder: [{ type: 'story', stateIds: ['a', 'c'] }],
        remove: [],
      });
    });

    it('rebaseStates_WhenTheTargetOfMyDeleteIsGone_ShouldPointItAtAnotherState', () => {
      const mine: StateChanges = {
        add: [],
        update: [],
        reorder: [],
        remove: [{ stateId: 'b', targetStateId: 'a' }],
      };
      const latest = saved.filter((s) => s.id !== 'a');
      expect(rebaseStates(latest, base, mine).remove).toEqual([
        { stateId: 'b', targetStateId: 'c' },
      ]);
    });

    it('rebaseStates_WhenTeammateRecategorisedWhatIRenamed_ShouldKeepTheirCategory', () => {
      const mine = editState(none(), base, 'b', { name: 'Doing' });
      const latest = saved.map((s) => (s.id === 'b' ? { ...s, category: 'resolved' as const } : s));
      expect(rebaseStates(latest, base, mine).update).toEqual([
        { stateId: 'b', name: 'Doing', category: 'resolved' },
      ]);
    });

    it('rebaseStates_WhenTeammateMadeTheSameEdit_ShouldDropIt', () => {
      const mine = editState(none(), base, 'b', { name: 'Doing' });
      const latest = saved.map((s) => (s.id === 'b' ? { ...s, name: 'Doing' } : s));
      expect(rebaseStates(latest, base, mine).update).toEqual([]);
    });

    it('rebaseStates_WhenNothingChangedUnderneath_ShouldKeepEverything', () => {
      const added = addState(none(), base, {
        id: 'n',
        type: 'story',
        name: 'x',
        category: 'inProgress',
      });
      const mine = removeState(editState(added, base, 'c', { name: 'Shipped' }), 'b', 'n');
      expect(rebaseStates(saved, base, mine)).toEqual(mine);
    });
  });
});
