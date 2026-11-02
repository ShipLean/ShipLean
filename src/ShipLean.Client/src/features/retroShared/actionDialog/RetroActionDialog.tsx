import { Dialog } from '../../../components/Dialog';
import type { RetroActionDialogQuery } from '../../../api/retrosApi';
import { RetroQueryState } from '../../retros/RetroQueryState';
import { useRetroActionDialogData } from '../../retros/useRetroQueries';
import { retroSharedTestIds as ids } from '../testIds';
import type { OpenRetroActionDialog } from './RetroActionDialogContext';
import { useRetroActionDialog } from './useRetroActionDialog';

function queryOf(request: OpenRetroActionDialog): RetroActionDialogQuery {
  const { scope } = request;
  return {
    pointIds: request.pointIds,
    themeId: request.themeId,
    itemId: scope.kind === 'item' ? scope.itemId : undefined,
    sprintId: scope.kind === 'sprint' ? scope.sprintId : undefined,
  };
}

// TODO: the New / Existing action item form, the points to cover and the real Save replace the title and Save below.
export function RetroActionDialog({
  request,
  onClose,
}: {
  request: OpenRetroActionDialog;
  onClose: () => void;
}) {
  const { projectId } = useRetroActionDialog();
  const query = useRetroActionDialogData(projectId, queryOf(request));
  return (
    <Dialog
      title="New action item"
      onClose={onClose}
      testId={ids.retroActionDialog}
      footer={
        <>
          <span className="flex-1" />
          <button type="button" className="cursor-pointer px-3 py-1 text-ink" onClick={onClose}>
            Cancel
          </button>
          <button
            type="button"
            data-testid={ids.retroActionSave}
            disabled
            className="rounded-control bg-accent px-3 py-1 text-signal-ink disabled:opacity-60"
          >
            Save
          </button>
        </>
      }
    >
      <RetroQueryState query={query}>
        {(dialog) => (
          <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
            {dialog.selected.length} selected
          </p>
        )}
      </RetroQueryState>
    </Dialog>
  );
}
