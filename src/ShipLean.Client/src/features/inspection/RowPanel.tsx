import { useRunRow } from './inspectionApi';
import { inspectionTestIds as ids } from './testIds';

export interface RowPanelProps {
  project: string;
  itemKey: string;
  runId: string;
  rowId: string;
}

// The side panel of the selected row; it fetches the row's detail itself.
// TODO: the panel's content replaces this stub.
export function RowPanel({ project, itemKey, runId, rowId }: RowPanelProps) {
  useRunRow(project, itemKey, runId, rowId);
  return <aside data-testid={ids.runPanel} />;
}
