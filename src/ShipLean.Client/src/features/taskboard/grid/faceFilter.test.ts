import { describe, expect, it } from 'vitest';
import { PRIYA, TOM, VIEW } from '../ct/fixtures';
import { UNASSIGNED_FACE, faceCounts } from './faceFilter';

describe('faceCounts', () => {
  it('CountsTasksPerPersonAndUnassigned_WhenGroupedByStories', () => {
    const counts = faceCounts(VIEW, 'stories');
    expect(counts.get(PRIYA.id)).toBe(1);
    expect(counts.get(TOM.id)).toBe(2);
    expect(counts.get(UNASSIGNED_FACE)).toBe(2);
  });

  it('UsesThePeopleRowsCounts_WhenGroupedByPeople', () => {
    const view = {
      rows: [],
      peopleRows: [
        { person: null, taskCount: 4 },
        { person: PRIYA, taskCount: 3 },
        { person: TOM, taskCount: 0 },
      ],
    };
    const counts = faceCounts(view, 'people');
    expect(counts.get(UNASSIGNED_FACE)).toBe(4);
    expect(counts.get(PRIYA.id)).toBe(3);
    expect(counts.get(TOM.id)).toBe(0);
  });
});
