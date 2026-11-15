import type { CardDto, TaskboardRowDto } from '@shiplean/core/api/views';
import { passesFaces } from '../faceFilter';

/** An open story (or the Unparented row) on one state's page: its tasks in that state that pass the filter. */
export interface StoryLane<Row extends TaskboardRowDto = TaskboardRowDto> {
  rowId: string;
  row: Row;
  /** The tasks in this state that pass the face filter. */
  tasks: CardDto[];
  /** The tasks in this state the face filter hides. */
  hidden: number;
}

/** A closed (completed) story: one line with its done/total tasks. */
export interface ClosedStory<Row extends TaskboardRowDto = TaskboardRowDto> {
  rowId: string;
  row: Row;
  done: number;
  total: number;
}

/** The id a story row goes by: its story's id, or `unparented`. */
export const storyRowId = (row: TaskboardRowDto): string => row.parent?.id ?? 'unparented';

const isClosed = (row: TaskboardRowDto) => row.parent?.state.category === 'completed';

/**
 * One state's page, grouped by Stories: every open story is a lane in backlog order (the rows' order), with its tasks in this
 * state or none (D4); closed stories follow, one line each, in backlog order (D8).
 */
export function phoneLanes<Row extends TaskboardRowDto>(
  rows: readonly Row[],
  stateId: string,
  faces: ReadonlySet<string>,
): { lanes: StoryLane<Row>[]; closed: ClosedStory<Row>[] } {
  const lanes = rows
    .filter((row) => !isClosed(row))
    .map((row) => {
      const all = row.cells.find((cell) => cell.stateId === stateId)?.tasks ?? [];
      const tasks = all.filter((card) => passesFaces(card, faces));
      return { rowId: storyRowId(row), row, tasks, hidden: all.length - tasks.length };
    });
  const closed = rows.filter(isClosed).map((row) => {
    const rollup = row.parent?.rollups.find((r) => r.type === 'task');
    const done = rollup?.closed ?? 0;
    return { rowId: storyRowId(row), row, done, total: done + (rollup?.open ?? 0) };
  });
  return { lanes, closed };
}
