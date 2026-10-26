import { describe, expect, it } from 'vitest';
import type { BoardColumnDto, BoardLaneDto } from '../../../api/views';
import { cellOf, mappedStates, showsLaneHeaders, tracksOf } from './layout';

const column = (id: string, split = false): BoardColumnDto => ({
  id,
  name: id,
  wipLimit: null,
  count: 0,
  hiddenCount: 0,
  overLimit: false,
  split,
  doingCount: 0,
  doneCount: 0,
  definitionOfDone: '',
  definitionOfDoneHtml: '',
  states: [],
});

const lane = (kind: BoardLaneDto['kind']): BoardLaneDto => ({
  id: kind,
  name: kind,
  colour: null,
  kind,
  criteriaText: null,
  epic: null,
  acceptsDrops: true,
  cardCount: 0,
  openPoints: 0,
  cells: [{ columnId: 'ready', doneHalf: true, cards: [], olderCount: 0, olderCursor: null }],
});

describe('layout', () => {
  it('tracksOf_WhenColumnSplit_ShouldGiveDoingAndDoneTracks', () => {
    const tracks = tracksOf([column('new'), column('ready', true), column('done')]);
    expect(tracks.map((t) => `${t.column.id}${t.doneHalf ? ':done' : ''}`)).toEqual([
      'new',
      'ready',
      'ready:done',
      'done',
    ]);
    expect(tracks.filter((t) => t.last).map((t) => t.column.id)).toEqual(['done']);
    expect(tracks.filter((t) => t.first).map((t) => t.column.id)).toEqual(['new']);
  });

  it('cellOf_WhenHalfDiffers_ShouldFindOnlyTheMatchingCell', () => {
    const tracks = tracksOf([column('ready', true)]);
    expect(cellOf(lane('default'), tracks[0]!)).toBeUndefined();
    expect(cellOf(lane('default'), tracks[1]!)).toBeDefined();
  });

  it('showsLaneHeaders_WhenOneDefaultLane_ShouldHideThem', () => {
    expect(showsLaneHeaders([lane('default')])).toBe(false);
    expect(showsLaneHeaders([lane('criteria'), lane('default')])).toBe(true);
    expect(showsLaneHeaders([lane('epic')])).toBe(true);
  });

  it('mappedStates_WhenTwoTypesShareAName_ShouldListItOnce', () => {
    const mapped = {
      ...column('new'),
      states: [
        { type: 'story', state: { id: '1', name: 'New', category: 'proposed' } },
        { type: 'bug', state: { id: '2', name: 'Triaged', category: 'proposed' } },
        { type: 'bug', state: { id: '3', name: 'New', category: 'proposed' } },
      ],
    } as BoardColumnDto;
    expect(mappedStates(mapped)).toBe('New · Triaged');
  });
});
