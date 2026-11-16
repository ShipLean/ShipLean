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

/** A closed (completed) story: one line with its done/total tasks, openable into its full lane. */
export interface ClosedStory<Row extends TaskboardRowDto = TaskboardRowDto> {
  rowId: string;
  row: Row;
  done: number;
  total: number;
  /** The story's lane on this page, drawn when its line is opened. */
  lane: StoryLane<Row>;
}

/** One entry of a page's list: an open story's lane or a closed story's line. */
export type PhoneEntry<Row extends TaskboardRowDto = TaskboardRowDto> =
  | { kind: 'lane'; lane: StoryLane<Row> }
  | { kind: 'closed'; story: ClosedStory<Row> };

/** The id a story row goes by: its story's id, or `unparented`. */
export const storyRowId = (row: TaskboardRowDto): string => row.parent?.id ?? 'unparented';

const isClosed = (row: TaskboardRowDto) => row.parent?.state.category === 'completed';

/**
 * One state's page, grouped by Stories: one ordered list in backlog order (the rows' order), an open story as a lane with its
 * tasks in this state or none (D4), a closed story as one line (D8), then the Unparented lane last whatever the input order (D12).
 * `lanes` and `closed` are the same entries split by kind.
 */
export function phoneLanes<Row extends TaskboardRowDto>(
  rows: readonly Row[],
  stateId: string,
  faces: ReadonlySet<string>,
): { entries: PhoneEntry<Row>[]; lanes: StoryLane<Row>[]; closed: ClosedStory<Row>[] } {
  const laneOf = (row: Row): StoryLane<Row> => {
    const all = row.cells.find((cell) => cell.stateId === stateId)?.tasks ?? [];
    const tasks = all.filter((card) => passesFaces(card, faces));
    return { rowId: storyRowId(row), row, tasks, hidden: all.length - tasks.length };
  };
  const entries = [...rows]
    .sort((a, b) => Number(a.parent === null) - Number(b.parent === null))
    .map((row): PhoneEntry<Row> => {
      const lane = laneOf(row);
      if (!isClosed(row)) return { kind: 'lane', lane };
      const rollup = row.parent?.rollups.find((r) => r.type === 'task');
      const done = rollup?.closed ?? 0;
      return {
        kind: 'closed',
        story: { rowId: lane.rowId, row, done, total: done + (rollup?.open ?? 0), lane },
      };
    });
  return {
    entries,
    lanes: entries.flatMap((e) => (e.kind === 'lane' ? [e.lane] : [])),
    closed: entries.flatMap((e) => (e.kind === 'closed' ? [e.story] : [])),
  };
}
