import { describe, expect, it } from 'vitest';
import { beforeItemIdAt, dropTargetOf, pagerStep } from './useTouchCardDrag';

const task = (key: string) => ({ id: `id-${key}`, key });
const view = {
  rows: [{ cells: [{ stateId: 's1', tasks: [task('SHIP-1'), task('SHIP-2')] }] }],
  peopleRows: [{ cells: [{ stateId: 's1', tasks: [task('SHIP-3')] }] }],
};

describe('useTouchCardDrag', () => {
  it('beforeItemIdAt_WhenGroupedByPeople_ShouldBeNull', () => {
    expect(beforeItemIdAt('people', view, 'SHIP-3', 'id-SHIP-1')).toBeNull();
  });

  it('beforeItemIdAt_WhenOverACardInStories_ShouldBeThatCardsId', () => {
    expect(beforeItemIdAt('stories', view, 'SHIP-2', 'id-SHIP-1')).toBe('id-SHIP-2');
  });

  it('beforeItemIdAt_WhenOverTheCardItself_ShouldBeNull', () => {
    expect(beforeItemIdAt('stories', view, 'SHIP-1', 'id-SHIP-1')).toBeNull();
  });

  it('beforeItemIdAt_WhenNoCardUnderTheFinger_ShouldBeNull', () => {
    expect(beforeItemIdAt('stories', view, null, 'id-SHIP-1')).toBeNull();
  });

  it('dropTargetOf_WhenNoTargetUnderTheFinger_ShouldBeNull', () => {
    expect(dropTargetOf(null)).toBeNull();
  });

  it('dropTargetOf_WhenTheTargetNamesARowAndAState_ShouldReadBoth', () => {
    const lane = document.createElement('div');
    Object.assign(lane.dataset, { rowId: 'story-7', stateId: 'active' });
    expect(dropTargetOf(lane)).toEqual({ rowId: 'story-7', stateId: 'active' });
  });

  it('pagerStep_WhenEdgeIsLeftOrRight_ShouldBeMinusOneOrOne', () => {
    expect([pagerStep('left'), pagerStep('right')]).toEqual([-1, 1]);
  });
});
