import { showToast } from '../../../components/toastStore';
import { useListCommand } from '../commands/listCommands';
import { openBelow } from './useAddChain';

// Registers `addBelow` for one row. Only the focused row mounts it, so the name belongs to that row.
export function AddBelowCommand({ rowId, filtering }: { rowId: string; filtering: boolean }) {
  useListCommand('addBelow', () => {
    if (filtering) {
      showToast(
        "Adding below a row is off while a filter is on, because rank is relative to rows you can't see.",
        { tone: 'danger' },
      );
      return;
    }
    openBelow(rowId);
  });
  return null;
}
