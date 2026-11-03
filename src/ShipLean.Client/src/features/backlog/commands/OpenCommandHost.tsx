import type { BacklogRowDto } from '../../../api/views';
import { useOpenItem } from '../../quickSearch/host/useOpenItem';
import { useSelection } from '../selection/useSelection';
import { useListCommand } from './listCommands';

// Registers the `open` command: it opens the focused row's item in the modal over the list. Mounted inside the
// selection provider, where the focus lives; `rows` are the shown item rows.
export function OpenCommandHost({
  rows,
  projectKey,
}: {
  rows: readonly BacklogRowDto[];
  projectKey: string;
}) {
  const selection = useSelection();
  const open = useOpenItem();
  useListCommand('open', () => {
    const row = rows.find((candidate) => candidate.id === selection.focusedId);
    if (row !== undefined) open({ key: row.key, projectKey });
  });
  return null;
}
