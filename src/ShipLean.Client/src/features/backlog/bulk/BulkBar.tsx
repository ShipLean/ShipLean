import type { BacklogViewDto } from '../../../api/views';

/** The backlog as the screen read it (`undefined` while it has not been), and the project, for the bar's actions. */
export interface BulkBarProps {
  mode: 'list' | 'planning';
  projectId: string;
  projectKey: string;
  view: BacklogViewDto | undefined;
}

// The action bar over a selection.
// TODO: the move, set and remove actions replace this empty bar.
export function BulkBar({ mode, projectId, projectKey, view }: BulkBarProps) {
  void [mode, projectId, projectKey, view];
  return null;
}
