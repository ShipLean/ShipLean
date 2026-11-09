import type { RetroPointDto } from '@shiplean/core/api/retros';
import { RetroActionDialogProvider } from '../../actionDialog/RetroActionDialogProvider';
import { RetroNote } from '../RetroNote';

/** Every state a note can be in, side by side as on design/screens/retro-notes.html. */
export function RetroNoteStatesHarness({ points }: { points: RetroPointDto[] }) {
  return (
    <RetroActionDialogProvider projectId="p1">
      <div
        data-testid="note-states"
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(4, 260px)',
          gap: 16,
          alignItems: 'start',
          padding: 12,
        }}
      >
        {points.map((point) => (
          <RetroNote key={point.id} point={point} showItem scope={{ kind: 'project' }} />
        ))}
      </div>
    </RetroActionDialogProvider>
  );
}
