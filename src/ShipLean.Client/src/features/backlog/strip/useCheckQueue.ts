import type { BacklogRowDto } from '../../../api/views';

export interface CheckQueue {
  /** The rows of the check being worked through, in their order. */
  apply: (rows: BacklogRowDto[]) => BacklogRowDto[];
  /** A check is being worked through. */
  active: boolean;
}

const identity = (rows: BacklogRowDto[]) => rows;
const off: CheckQueue = { apply: identity, active: false };

/** Working through the rows one "Before you plan" check flags. */
// TODO: the queue of a clicked check replaces this pass-through.
export function useCheckQueue(): CheckQueue {
  return off;
}
