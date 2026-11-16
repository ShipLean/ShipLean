import type { RetroPointDto } from '@shiplean/core/api/retros';
import { retroCategoryLabels } from '../retroCategories';
import { retroSharedTestIds as ids } from '../testIds';

/** The point as the two note sheets show it: its text in a tinted box, with its category (and `meta`) under it. */
export function NotePreview({ point, meta }: { point: RetroPointDto; meta?: string }) {
  return (
    <div
      data-testid={ids.retroNotePreview}
      className="mb-1 grid gap-1 rounded-control bg-sunken px-3 py-2 text-[length:var(--text-base)] text-ink [overflow-wrap:anywhere]"
    >
      {point.text}
      <small className="text-[length:var(--text-sm)] text-ink-muted">
        {retroCategoryLabels[point.category]}
        {meta ? ` · ${meta}` : ''}
      </small>
    </div>
  );
}
