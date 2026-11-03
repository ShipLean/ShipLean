import type { RetroCategory, RetroPointDto } from '../../../api/retros';
import { retroSharedTestIds as ids } from '../testIds';
import type { AddTarget } from './AddTarget';
import { NoteEditor } from './NoteEditor';

export interface AddNoteProps {
  category: RetroCategory;
  add: AddTarget;
  /** True while this column's editor is the board's one open editor. */
  open: boolean;
  onOpen: () => void;
  onClose: () => void;
  onAdded?: (point: RetroPointDto) => void;
}

/** Under a column's last card: the dashed `+ Add` button, or the blank note it opens. */
export function AddNote({ category, add, open, onOpen, onClose, onAdded }: AddNoteProps) {
  if (open) {
    return <NoteEditor category={category} add={add} onClose={onClose} onAdded={onAdded} />;
  }
  return (
    <button
      type="button"
      data-testid={ids.retroBoardAdd}
      data-category={category}
      style={{ borderColor: 'color-mix(in srgb, var(--cat) 45%, var(--color-edge))' }}
      className="h-7 w-full cursor-pointer rounded-sm border border-dashed bg-transparent text-[length:var(--text-sm)] text-ink-muted hover:border-solid hover:bg-surface hover:text-ink"
      onClick={onOpen}
    >
      + Add
    </button>
  );
}
