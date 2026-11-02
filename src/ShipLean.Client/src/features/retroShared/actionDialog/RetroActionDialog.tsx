import { Dialog } from '../../../components/Dialog';
import type { RetroActionDialogQuery } from '../../../api/retrosApi';
import { RetroQueryState } from '../../retros/RetroQueryState';
import { useRetroActionDialogData } from '../../retros/useRetroQueries';
import { retroSharedTestIds as ids } from '../testIds';
import type { OpenRetroActionDialog } from './RetroActionDialogContext';
import { RetroActionForm } from './RetroActionForm';
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

/** Loads what the dialog offers, then shows the form; until then, and if the load fails, an empty frame to close. */
export function RetroActionDialog({
  request,
  onClose,
}: {
  request: OpenRetroActionDialog;
  onClose: () => void;
}) {
  const { projectId } = useRetroActionDialog();
  const query = useRetroActionDialogData(projectId, queryOf(request));
  if (query.data === undefined) {
    return (
      <Dialog title="Action item" width="620px" onClose={onClose} testId={ids.retroActionDialog}>
        <RetroQueryState query={query}>{() => null}</RetroQueryState>
      </Dialog>
    );
  }
  return <RetroActionForm request={request} dialog={query.data} onClose={onClose} />;
}
