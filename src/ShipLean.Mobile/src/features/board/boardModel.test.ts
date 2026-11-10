import type { StateCategory } from '@shiplean/core/api/refs';
import type { FeatureBoardRowDto, FeatureBoardViewDto } from '@shiplean/core/api/views';
import { describe, expect, it } from 'vitest';
import { designBoard } from '../../test-support/features';
import { boardFigures, groupRows, sectionLayout, taskBar } from './boardModel';

const board = (): FeatureBoardViewDto => designBoard();

function withRow(
  view: FeatureBoardViewDto,
  base: FeatureBoardRowDto,
  patch: { type?: 'story' | 'bug'; category?: StateCategory },
): FeatureBoardRowDto {
  const row = structuredClone(base);
  if (patch.type) row.parent.type = { id: patch.type, name: patch.type };
  if (patch.category) {
    row.parent.state = { ...row.parent.state, category: patch.category };
  }
  view.rows.push(row);
  return row;
}

describe('feature board model', () => {
  it('counts stories and bugs apart', () => {
    const view = board();
    // The fixture: stories SHIP-141, 144 (open), SHIP-129 (open), SHIP-150 (closed); bug SHIP-138 (closed).
    const figures = boardFigures(view);
    expect(figures.stories).toEqual({ closed: 1, total: 4 });
    expect(figures.bugs).toEqual({ closed: 1, total: 1 });
    // The header's own storiesClosed and storiesTotal count both together and are not what the figures show.
    expect(view.header.storiesTotal).toBe(5);
    expect(figures.tasks).toEqual({
      closed: view.header.tasksClosed,
      total: view.header.tasksTotal,
    });
    expect(figures.points).toEqual({ closed: 5, total: 21 });
  });

  it('counts a removed story as closed and gives no points when none have any', () => {
    const view = board();
    withRow(view, view.rows[0], { type: 'story', category: 'removed' });
    view.header.pointsTotal = null;
    const figures = boardFigures(view);
    expect(figures.stories).toEqual({ closed: 2, total: 5 });
    expect(figures.points).toBeNull();
  });

  it('splits tasks into done, doing and to do by column category', () => {
    const view = board();
    const row = view.rows[0];
    const column = (category: StateCategory) =>
      view.columns.find((c) => c.state.category === category)!.state.id;
    const task = row.cells[0].tasks[0];
    row.cells = [
      { stateId: column('proposed'), tasks: [task] },
      { stateId: column('inProgress'), tasks: [task, task] },
      { stateId: column('resolved'), tasks: [task] },
      { stateId: column('completed'), tasks: [task, task, task] },
    ];

    expect(taskBar(view, row)).toEqual({ done: 3, doing: 3, todo: 1 });
  });

  it('orders groups Active, Resolved, New, Closed', () => {
    const view = board();
    const base = view.rows[0];
    view.rows = [];
    withRow(view, base, { category: 'completed' });
    withRow(view, base, { category: 'proposed' });
    withRow(view, base, { category: 'resolved' });
    withRow(view, base, { category: 'inProgress' });
    withRow(view, base, { category: 'removed' });

    const groups = groupRows(view.rows);

    expect(groups.map((g) => [g.category, g.label, g.rows.length, g.folded])).toEqual([
      ['inProgress', 'Active', 1, false],
      ['resolved', 'Resolved', 1, false],
      ['proposed', 'New', 1, false],
      ['completed', 'Closed', 2, true],
    ]);
  });

  it('lays out sections depth-first and leaves out an empty board section', () => {
    const view = board();
    const layout = sectionLayout(view);
    expect(layout.map((l) => l.section.key)).toEqual(view.sections.map((s) => s.key));
    expect(layout[0].direct).toBe(true);
    view.rows = view.rows.filter((row) => row.featureId !== view.sections[0].id);
    expect(sectionLayout(view).map((l) => l.section.key)).toEqual(
      view.sections.slice(1).map((s) => s.key),
    );
    view.sections = view.sections.slice(0, 1);
    expect(sectionLayout(view)).toEqual([]);
  });
});
