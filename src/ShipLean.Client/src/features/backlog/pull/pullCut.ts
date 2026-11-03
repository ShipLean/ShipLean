import { BULK_MAX_ROWS } from '../../../api/backlogWrites';

export interface PullCut {
  /** How many rows from the top of the stack go into the sprint. */
  selected: number;
  /** The sprint's points after those rows join it. */
  sprintPoints: number;
  /** How far over `room` the sprint ends up; null when it is not over. */
  overBy: number | null;
  /** The cut stopped at the row cap, not at a row that did not fit. */
  capped: boolean;
}

/**
 * The forecast's cut: take rows from the top of the stack while they fit the room left after what the sprint already
 * holds, stop at the first that does not and never skip. A row bigger than the room still lands in a sprint that is
 * otherwise empty. Unestimated rows count zero. The same rule as SprintFill.Cut, pinned by pull-cut-cases.json.
 */
export function pullCut(
  stack: readonly (number | null)[],
  room: number,
  alreadyIn: number,
  cap: number = BULK_MAX_ROWS,
): PullCut {
  let used = alreadyIn;
  let selected = 0;
  let capped = false;
  for (const row of stack) {
    if (selected === cap) {
      capped = true;
      break;
    }
    const points = row ?? 0;
    if (used + points <= room || (selected === 0 && alreadyIn === 0)) {
      selected++;
      used += points;
      continue;
    }
    break;
  }
  return { selected, sprintPoints: used, overBy: used > room ? used - room : null, capped };
}
