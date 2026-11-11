import type { StateCategory } from '@shiplean/core/api/refs';
import type { CardDto, FeatureBoardRowDto, FeatureBoardViewDto } from '@shiplean/core/api/views';

// Counts of one Story's open tasks on a Feature's board, and the tasks that are blocked. Done comes from the item's roll-ups, so completed and removed
// tasks are left out here.

export interface BlockedTask {
  key: string;
  title: string;
  // Null when the task has no owner.
  owner: string | null;
}

export interface TaskCounts {
  // Resolved counts as in progress, as the web's task board draws it.
  inProgress: number;
  todo: number;
  // Open tasks carrying the Blocked tag; they are not counted again as in progress or to do.
  blocked: BlockedTask[];
}

const OPEN: readonly StateCategory[] = ['proposed', 'inProgress', 'resolved'];

/** A task is blocked when its card has the tag Blocked, in any case. */
export function isBlocked(card: CardDto): boolean {
  return card.tags.some((tag) => tag.name.toLowerCase() === 'blocked');
}

function openTasks(
  row: FeatureBoardRowDto,
  board: Pick<FeatureBoardViewDto, 'columns'>,
): { card: CardDto; category: StateCategory }[] {
  const categoryOf = new Map(board.columns.map((c) => [c.state.id, c.state.category]));
  return row.cells.flatMap((cell) => {
    const category = categoryOf.get(cell.stateId);
    return category !== undefined && OPEN.includes(category)
      ? cell.tasks.map((card) => ({ card, category }))
      : [];
  });
}

const blockedTask = (card: CardDto): BlockedTask => ({
  key: card.key,
  title: card.title,
  owner: card.assignee?.name ?? null,
});

/** Splits a Story's tasks by the category of the column they sit in, and names the blocked ones. */
export function taskCounts(
  row: FeatureBoardRowDto,
  board: Pick<FeatureBoardViewDto, 'columns'>,
): TaskCounts {
  const counts: TaskCounts = { inProgress: 0, todo: 0, blocked: [] };
  for (const { card, category } of openTasks(row, board)) {
    if (isBlocked(card)) counts.blocked.push(blockedTask(card));
    else if (category === 'proposed') counts.todo += 1;
    else counts.inProgress += 1;
  }
  return counts;
}

/** Every blocked open task on the board, for a Feature's header. */
export function blockedOnBoard(board: FeatureBoardViewDto): BlockedTask[] {
  return board.rows.flatMap((row) => taskCounts(row, board).blocked);
}
