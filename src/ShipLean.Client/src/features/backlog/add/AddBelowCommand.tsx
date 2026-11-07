import { showToast } from '../../../components/toastStore';
import { useListCommand } from '../commands/listCommands';
import { useBelowControls } from './useAddChain';

// Registers `addBelow` for one row. Only the focused row mounts it, so the name belongs to that row.
export function AddBelowCommand({
  rowId,
  filtering,
  sorted,
}: {
  rowId: string;
  filtering: boolean;
  sorted: boolean;
}) {
  const below = useBelowControls();
  useListCommand('addBelow', () => {
    if (filtering) {
      showToast(
        "Adding below a row is off while a filter is on, because rank is relative to rows you can't see.",
        { tone: 'danger' },
      );
      return;
    }
    if (sorted) {
      showToast(
        'Adding below a row is off while the list is sorted by a column. Click the sorted title until the arrow goes to get rank order back.',
        { tone: 'danger' },
      );
      return;
    }
    below.open(rowId);
  });
  return null;
}
