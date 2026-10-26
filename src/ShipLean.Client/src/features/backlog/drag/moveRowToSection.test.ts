import { describe, expect, it } from 'vitest';
import { planningView } from '../ct/fixtures';
import { withWritten } from '../planning/withWritten';
import { moveRowToSection } from './useReorder';

const view = planningView();
const keys = (v: typeof view, sectionId: string) =>
  v.sections.find((s) => s.id === sectionId)?.rows.map((r) => r.key);
const section = (v: typeof view, sectionId: string) => v.sections.find((s) => s.id === sectionId)!;

describe('moveRowToSection', () => {
  it('Move_WhenIntoAnotherSectionAboveARow_ShouldTakeItsSprintAndPlace', () => {
    const moved = moveRowToSection(view, 'id-SHIP-160', 'backlog', 'id-SHIP-171');

    expect(keys(moved, 'sp24')).toEqual([]);
    expect(keys(moved, 'backlog')).toEqual(['SHIP-170', 'SHIP-160', 'SHIP-171', 'SHIP-172']);
    expect(section(moved, 'backlog').rows[1].sprint).toBeNull();
  });

  it('Move_WhenIntoASprintLast_ShouldSetTheSprintAndGoLast', () => {
    const moved = moveRowToSection(view, 'id-SHIP-170', 'sp25', null);

    expect(keys(moved, 'sp25')).toEqual(['SHIP-158', 'SHIP-170']);
    expect(section(moved, 'sp25').rows[1].sprint).toEqual(section(view, 'sp25').sprint);
  });

  it('Move_WhenIntoAnEmptySection_ShouldFillIt', () => {
    expect(keys(moveRowToSection(view, 'id-SHIP-170', 'sp26', null), 'sp26')).toEqual(['SHIP-170']);
  });

  it('Move_WhenBetweenSections_ShouldMoveThePointsWithTheRow', () => {
    const moved = moveRowToSection(view, 'id-SHIP-171', 'sp25', null);

    expect(section(moved, 'backlog').points).toBe(8);
    expect(section(moved, 'sp25').points).toBe(16);
  });

  it('Move_WhenWithinOneSection_ShouldKeepThePoints', () => {
    const moved = moveRowToSection(view, 'id-SHIP-172', 'backlog', 'id-SHIP-170');

    expect(keys(moved, 'backlog')).toEqual(['SHIP-172', 'SHIP-170', 'SHIP-171']);
    expect(section(moved, 'backlog').points).toBe(16);
  });

  it('Move_WhenTheRowIsUnknown_ShouldChangeNothing', () => {
    expect(moveRowToSection(view, 'nope', 'sp25', null)).toBe(view);
  });
});

describe('withWritten', () => {
  it('Written_ShouldPutTheRevisionAndSprintOnTheRow', () => {
    const sprint = section(view, 'sp25').sprint;
    const next = withWritten(view, 'id-SHIP-170', 9, sprint);
    const row = section(next, 'backlog').rows[0];

    expect(row.revision).toBe(9);
    expect(row.sprint).toEqual(sprint);
    expect(section(next, 'backlog').rows[1].revision).toBe(1);
  });
});
