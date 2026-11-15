import { useRetroActionDialog } from '../../../retroShared/actionDialog/useRetroActionDialog';
import { retrosTestIds as ids } from '../../testIds';

/** + Action item in the Action items header: opens the dialog in New mode with no points ticked; the project's undecided points are offered. */
export function AddActionItemButton() {
  const dialog = useRetroActionDialog();
  return (
    <button
      type="button"
      data-testid={ids.retroActionsAdd}
      className="btn-ghost cursor-pointer whitespace-nowrap touch:min-h-[var(--touch-target)]"
      onClick={() => dialog.open({ scope: { kind: 'project' } })}
    >
      + Action item
    </button>
  );
}
