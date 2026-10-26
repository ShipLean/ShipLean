import { describe, expect, it } from 'vitest';
import type { CriteriaClause, SwimlaneJson } from '../../../../api/persisted';
import { acceptsDrops, blankLane, moveLane, withCriteria } from './laneEdits';

const lane = (id: string, clauses: CriteriaClause[] | null): SwimlaneJson => ({
  id,
  name: id,
  colour: null,
  kind: clauses === null ? 'manual' : 'criteria',
  criteria: clauses === null ? null : { match: 'all', clauses },
});
const clause = (field: string, op: CriteriaClause['op'], value: CriteriaClause['value']) => ({
  field,
  op,
  value,
});

describe('acceptsDrops', () => {
  it('is true for a manual lane and the default lane', () => {
    expect(acceptsDrops(lane('m', null))).toBe(true);
    expect(acceptsDrops({ ...lane('d', null), kind: 'default' })).toBe(true);
  });

  it('is true when every clause is = on a droppable field', () => {
    expect(
      acceptsDrops(lane('a', [clause('priority', '=', 1), clause('assignee', '=', '@me')])),
    ).toBe(true);
  });

  it('is false for a criteria lane with no clauses', () => {
    expect(acceptsDrops(lane('a', []))).toBe(false);
    expect(acceptsDrops({ ...lane('a', []), criteria: null })).toBe(false);
  });

  it('is false for another operator on a droppable field', () => {
    expect(acceptsDrops(lane('a', [clause('priority', '<', 2)]))).toBe(false);
  });

  it('is false for = on a field the board cannot write', () => {
    expect(acceptsDrops(lane('a', [clause('priority', '=', 1), clause('tags', '=', 'x')]))).toBe(
      false,
    );
  });
});

describe('moveLane', () => {
  const lanes = ['a', 'b', 'c'].map((id) => lane(id, null));
  it('moves a lane to the new index', () => {
    expect(moveLane(lanes, 0, 2).map((l) => l.id)).toEqual(['b', 'c', 'a']);
  });
  it('ignores a move off either end', () => {
    expect(moveLane(lanes, 0, -1)).toBe(lanes);
    expect(moveLane(lanes, 2, 3)).toBe(lanes);
  });
});

describe('withCriteria', () => {
  it('turns an empty clause list into no criteria', () => {
    expect(
      withCriteria(lane('a', [clause('priority', '=', 1)]), { match: 'all', clauses: [] }).criteria,
    ).toBeNull();
  });
});

describe('blankLane', () => {
  it('is a manual lane with its own id', () => {
    const a = blankLane();
    expect(a.kind).toBe('manual');
    expect(a.id).not.toBe(blankLane().id);
  });
});
