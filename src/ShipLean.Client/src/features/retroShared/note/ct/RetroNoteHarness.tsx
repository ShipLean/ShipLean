import type { RetroPointDto } from '@shiplean/core/api/retros';
import { ToastHost } from '../../../../components/ToastHost';
import { RetroActionDialogProvider } from '../../actionDialog/RetroActionDialogProvider';
import { RetroNote } from '../RetroNote';

/** One note inside the provider; `tall` puts it between two screens of space so scrolling to it can be seen. */
export function RetroNoteHarness({
  point,
  highlighted = false,
  tall = false,
}: {
  point: RetroPointDto;
  highlighted?: boolean;
  tall?: boolean;
}) {
  return (
    <RetroActionDialogProvider projectId="p1">
      {tall ? <div style={{ height: 3000 }} /> : null}
      <RetroNote point={point} highlighted={highlighted} scope={{ kind: 'project' }} />
      {tall ? <div style={{ height: 3000 }} /> : null}
      <ToastHost />
    </RetroActionDialogProvider>
  );
}
