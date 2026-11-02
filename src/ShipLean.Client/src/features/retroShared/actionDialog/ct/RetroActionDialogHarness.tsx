import { ToastHost } from '../../../../components/ToastHost';
import { RetroActionDialogProvider } from '../RetroActionDialogProvider';
import { useRetroActionDialog } from '../useRetroActionDialog';

interface Opening {
  pointIds?: string[];
  existingWorkItemId?: string;
}

function Opener({ pointIds, existingWorkItemId }: Opening) {
  const dialog = useRetroActionDialog();
  return (
    <button
      type="button"
      onClick={() =>
        dialog.open({ pointIds, existingWorkItemId, scope: { kind: 'item', itemId: 'item-1' } })
      }
    >
      open
    </button>
  );
}

/** A provider with a button that opens the dialog in an item's scope; the points and the item to add to are props. */
export function RetroActionDialogHarness({
  pointIds = ['pt-1', 'pt-2'],
  existingWorkItemId,
}: Opening) {
  return (
    <RetroActionDialogProvider projectId="p1">
      <Opener pointIds={pointIds} existingWorkItemId={existingWorkItemId} />
      <ToastHost />
    </RetroActionDialogProvider>
  );
}
