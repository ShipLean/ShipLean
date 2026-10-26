import { describe, expect, it } from 'vitest';
import { VIEW } from '../ct/fixtures';
import { PRIYA } from '../ct/fixtures';
import { applyMove, offerResolveOn, revisionOf, undoOf, withRevision } from './applyMove';

const keysIn = (cells: { stateId: string; tasks: { key: string }[] }[], stateId: string) =>
  cells.find((c) => c.stateId === stateId)?.tasks.map((t) => t.key);

describe('applyMove', () => {
  it('applyMove_WhenDroppedInAnotherColumn_ShouldMoveTheCardAndRecountColumns', () => {
    const next = applyMove(VIEW, 'stories', {
      taskId: 'id-SHIP-173',
      stateId: 't-active',
      rowId: 'id-SHIP-141',
      beforeItemId: null,
    });
    expect(keysIn(next.rows[0].cells, 't-new')).toEqual([]);
    expect(keysIn(next.rows[0].cells, 't-active')).toEqual(['SHIP-172', 'SHIP-173']);
    expect(
      next.rows[0].cells.flatMap((c) => c.tasks).find((t) => t.key === 'SHIP-173')?.state.id,
    ).toBe('t-active');
    expect(next.columns.map((c) => c.count)).toEqual([1, 2, 2]);
  });

  it('applyMove_WhenDroppedAboveCard_ShouldInsertBeforeIt', () => {
    const next = applyMove(VIEW, 'stories', {
      taskId: 'id-SHIP-173',
      stateId: 't-active',
      rowId: 'id-SHIP-141',
      beforeItemId: 'id-SHIP-172',
    });
    expect(keysIn(next.rows[0].cells, 't-active')).toEqual(['SHIP-173', 'SHIP-172']);
  });

  it('applyMove_WhenDroppedOnAnotherStoryRow_ShouldReparentTheCard', () => {
    const next = applyMove(VIEW, 'stories', {
      taskId: 'id-SHIP-182',
      stateId: 't-new',
      rowId: 'id-SHIP-141',
      beforeItemId: null,
    });
    const moved = next.rows[0].cells.flatMap((c) => c.tasks).find((t) => t.key === 'SHIP-182');
    expect(moved?.parent?.key).toBe('SHIP-141');
    expect(next.rows[2].cells.flatMap((c) => c.tasks)).toEqual([]);
  });

  it('applyMove_WhenDroppedOnUnparentedRow_ShouldClearTheParent', () => {
    const next = applyMove(VIEW, 'stories', {
      taskId: 'id-SHIP-173',
      stateId: 't-new',
      rowId: 'unparented',
      beforeItemId: null,
    });
    const moved = next.rows[2].cells.flatMap((c) => c.tasks).find((t) => t.key === 'SHIP-173');
    expect(moved?.parent).toBeNull();
  });

  it('applyMove_WhenDroppedOnPersonRow_ShouldAssignAndRecountTheRows', () => {
    const next = applyMove(VIEW, 'people', {
      taskId: 'id-SHIP-173',
      stateId: 't-active',
      rowId: 'u-priya',
      beforeItemId: null,
    });
    const priya = next.peopleRows[1];
    expect(keysIn(priya.cells, 't-active')).toEqual(['SHIP-173']);
    expect(
      priya.cells.flatMap((c) => c.tasks).find((t) => t.key === 'SHIP-173')?.assignee?.id,
    ).toBe('u-priya');
    expect(next.peopleRows.map((r) => r.taskCount)).toEqual([1, 2, 2]);
  });

  it('applyMove_WhenDestinationRowIsNotThere_ShouldLeaveTheViewAsItIs', () => {
    const next = applyMove(VIEW, 'people', {
      taskId: 'id-SHIP-173',
      stateId: 't-active',
      rowId: 'u-nobody',
      beforeItemId: null,
    });
    expect(next).toBe(VIEW);
  });

  it('applyMove_WhenStateIsNotAColumn_ShouldLeaveTheViewAsItIs', () => {
    expect(
      applyMove(VIEW, 'stories', {
        taskId: 'id-SHIP-173',
        stateId: 'nope',
        rowId: 'unparented',
        beforeItemId: null,
      }),
    ).toBe(VIEW);
  });
});

describe('offerResolveOn', () => {
  it('offerResolveOn_WhenParentGiven_ShouldFlagOnlyThatRow', () => {
    const next = offerResolveOn(VIEW, 'id-SHIP-141');
    expect(next.rows.map((r) => r.offerResolve)).toEqual([true, false, false]);
  });
});

describe('applyMove assignee, revision and undo', () => {
  it('applyMove_WhenAssigneeGivenInStories_ShouldSetItOnTheCard', () => {
    const next = applyMove(VIEW, 'stories', {
      taskId: 'id-SHIP-173',
      stateId: 't-active',
      rowId: 'id-SHIP-141',
      beforeItemId: null,
      assignee: PRIYA,
    });
    const card = next.rows[0].cells.flatMap((c) => c.tasks).find((t) => t.key === 'SHIP-173');
    expect(card?.assignee).toEqual(PRIYA);
  });

  it('applyMove_WhenMyPeopleRowIsMissing_ShouldMakeItAndPutTheCardIn', () => {
    const view = { ...VIEW, peopleRows: VIEW.peopleRows.filter((r) => r.person?.id !== PRIYA.id) };
    const next = applyMove(view, 'people', {
      taskId: 'id-SHIP-173',
      stateId: 't-active',
      rowId: PRIYA.id,
      beforeItemId: null,
      assignee: PRIYA,
    });
    const mine = next.peopleRows.find((r) => r.person?.id === PRIYA.id);
    expect(keysIn(mine?.cells ?? [], 't-active')).toEqual(['SHIP-173']);
    expect(mine?.taskCount).toBe(1);
  });

  it('applyMove_WhenUndone_ShouldPutTheCardBackWithItsAssignee', () => {
    const picked = applyMove(VIEW, 'stories', {
      taskId: 'id-SHIP-173',
      stateId: 't-active',
      rowId: 'id-SHIP-141',
      beforeItemId: null,
      assignee: PRIYA,
    });
    const original = VIEW.rows[0].cells.flatMap((c) => c.tasks).find((t) => t.key === 'SHIP-173')!;
    const back = applyMove(picked, 'stories', undoOf(original, 'id-SHIP-141'));
    const card = back.rows[0].cells.flatMap((c) => c.tasks).find((t) => t.key === 'SHIP-173');
    expect(card?.state.id).toBe('t-new');
    expect(card?.assignee).toBeNull();
  });

  it('withRevision_WhenWritten_ShouldReplaceTheCardsRevisionEverywhere', () => {
    const next = withRevision(VIEW, 'id-SHIP-173', 7);
    expect(revisionOf(next, 'id-SHIP-173')).toBe(7);
    expect(revisionOf(next.peopleRows.length ? { ...next, rows: [] } : next, 'id-SHIP-173')).toBe(
      7,
    );
    expect(revisionOf(next, 'id-SHIP-172')).toBe(1);
  });
});
