import { describe, expect, it } from 'vitest';
import { storiesBoard } from '../../ct/fixtures';
import {
  dropAtBottom,
  dropAtEnd,
  dropAtTop,
  dropInLane,
  moveTargets,
  otherLanes,
} from './moveTargets';

const view = storiesBoard();

describe('moveTargets', () => {
  it('moveTargets_WhenSplitColumn_ShouldOfferDoingAndDone', () => {
    const targets = moveTargets(view, 'id-SHIP-160');
    expect(targets.map((t) => [t.columnId, t.done, t.label])).toEqual([
      ['col-new', false, 'New'],
      ['col-ready', false, 'Ready · Doing'],
      ['col-ready', true, 'Ready · Done'],
      ['col-prog', false, 'In progress'],
      ['col-done', false, 'Done'],
    ]);
    expect(targets.find((t) => t.columnId === 'col-prog')?.wip).toBe('3 of 2 (over)');
    expect(targets.find((t) => t.columnId === 'col-new')?.current).toBe(true);
    expect(targets.filter((t) => t.current)).toHaveLength(1);
  });

  it('moveTargets_WhenMovedToColumn_ShouldTargetEnd', () => {
    expect(dropAtEnd(view, 'id-SHIP-160', { columnId: 'col-ready', done: true })).toEqual({
      laneId: 'lane-default',
      columnId: 'col-ready',
      done: true,
      beforeItemId: null,
    });
    expect(dropAtEnd(view, 'id-nope', { columnId: 'col-ready', done: true })).toBeNull();
  });

  it('moveTargets_WhenLanePicked_ShouldKeepColumnAndHalf', () => {
    expect(otherLanes(view, 'id-SHIP-160').map((lane) => lane.id)).toEqual(['lane-expedite']);
    expect(dropInLane(view, 'id-SHIP-162', 'lane-expedite')).toEqual({
      laneId: 'lane-expedite',
      columnId: 'col-ready',
      done: true,
      beforeItemId: null,
    });
  });

  it('moveTargets_WhenOnlyCardInCell_ShouldOfferNoTopOrBottom', () => {
    expect(dropAtTop(view, 'id-SHIP-160')).toBeNull();
    expect(dropAtBottom(view, 'id-SHIP-160')).toBeNull();
  });
});
