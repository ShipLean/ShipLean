import type { BacklogRowDto } from '../../../api/views';

export interface AddBelowRowProps {
  /** The row this sits under. */
  row: BacklogRowDto;
  columnCount: number;
  projectId: string;
  projectKey: string;
  /** A filter, search or check queue is on: rank is relative to rows you can't see. */
  filtering: boolean;
}

// The inline add row under the focused row. One is mounted under every row; it shows only under the focused one.
// TODO: the add-below row (title, parent, points) replaces this empty one.
export function AddBelowRow(props: AddBelowRowProps) {
  void props;
  return null;
}
