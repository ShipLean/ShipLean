import type { RetroCategory, RetroPointDto } from '../../../api/retros';
import { retroSharedTestIds as ids } from '../testIds';
import type { AddTarget } from './AddTarget';

export interface AddNoteProps {
  category: RetroCategory;
  add: AddTarget;
  onAdded?: (point: RetroPointDto) => void;
}

// TODO: the inline editor that adds a point to this column replaces this stub; it uses `add` and calls `onAdded`.
export function AddNote({ category }: AddNoteProps) {
  return (
    <button
      type="button"
      data-testid={ids.retroBoardAdd}
      data-category={category}
      disabled
      className="h-7 w-full rounded-sm border border-dashed border-edge bg-transparent text-[length:var(--text-sm)] text-ink-muted"
    >
      + Add
    </button>
  );
}
