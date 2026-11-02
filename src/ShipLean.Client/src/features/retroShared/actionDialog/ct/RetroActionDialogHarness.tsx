import { RetroActionDialogProvider } from '../RetroActionDialogProvider';
import { useRetroActionDialog } from '../useRetroActionDialog';

function Opener() {
  const dialog = useRetroActionDialog();
  return (
    <button
      type="button"
      onClick={() =>
        dialog.open({ pointIds: ['pt-1', 'pt-2'], scope: { kind: 'item', itemId: 'item-1' } })
      }
    >
      open
    </button>
  );
}

/** A provider with a button that opens the dialog on two points in an item's scope. */
export function RetroActionDialogHarness() {
  return (
    <RetroActionDialogProvider projectId="p1">
      <Opener />
    </RetroActionDialogProvider>
  );
}
