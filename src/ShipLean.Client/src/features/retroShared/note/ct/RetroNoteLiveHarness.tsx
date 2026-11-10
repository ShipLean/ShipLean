import type { RetroPointDto } from '@shiplean/core/api/retros';
import { ToastHost } from '../../../../components/ToastHost';
import { RetroActionDialogProvider } from '../../actionDialog/RetroActionDialogProvider';
import { useRetroItem } from '../../../retros/useRetroQueries';
import { RetroNote } from '../RetroNote';

function LiveNote({ itemKey, pointId }: { itemKey: string; pointId: string }) {
  const item = useRetroItem('p1', itemKey).data;
  const point: RetroPointDto | undefined = item?.categories
    .flatMap((column) => column.groups.flatMap((group) => group.points))
    .find((candidate) => candidate.id === pointId);
  return point ? <RetroNote point={point} scope={{ kind: 'project' }} /> : null;
}

/** A note fed the way a screen feeds it: the point comes from the retro item query, so a write only changes the note when the refetch does. */
export function RetroNoteLiveHarness({ itemKey, pointId }: { itemKey: string; pointId: string }) {
  return (
    <RetroActionDialogProvider projectId="p1">
      <LiveNote itemKey={itemKey} pointId={pointId} />
      <ToastHost />
    </RetroActionDialogProvider>
  );
}
