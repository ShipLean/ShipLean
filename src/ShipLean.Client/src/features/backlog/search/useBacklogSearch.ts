import type { BacklogRowDto } from '../../../api/views';

export interface BacklogSearch {
  /** The rows that match, in their order. */
  apply: (rows: BacklogRowDto[]) => BacklogRowDto[];
  /** A search is narrowing the rows. */
  active: boolean;
}

const identity = (rows: BacklogRowDto[]) => rows;
const off: BacklogSearch = { apply: identity, active: false };

/** The screen's search over its rows. */
// TODO: the query, matching and the search command replace this pass-through.
export function useBacklogSearch(): BacklogSearch {
  return off;
}
