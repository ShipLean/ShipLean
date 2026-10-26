import { useState } from 'react';
import type { LinkCardDto } from '../../../../api/linkCards';
import { inputClass } from '../../../workItem/form/inputClasses';
import { featureTestIds as ids } from '../../testIds';

// Edit title and note in place. The draft stays put when the save fails, so nothing typed is lost.
export function LinkCardEditor({
  card,
  saving,
  onSave,
  onCancel,
}: {
  card: LinkCardDto;
  saving: boolean;
  onSave: (title: string, note: string) => void;
  onCancel: () => void;
}) {
  const [title, setTitle] = useState(card.title);
  const [note, setNote] = useState(card.note ?? '');
  return (
    <div className="grid gap-1.5">
      <input
        data-testid={ids.linkCardTitleInput}
        aria-label="Title"
        value={title}
        className={inputClass}
        onChange={(event) => setTitle(event.target.value)}
      />
      <input
        data-testid={ids.linkCardNoteInput}
        aria-label="Note"
        placeholder="Why this link matters"
        value={note}
        className={inputClass}
        onChange={(event) => setNote(event.target.value)}
      />
      <div className="flex gap-2">
        <button
          type="button"
          data-testid={ids.linkCardSave}
          disabled={saving}
          className="h-7 rounded-control bg-signal px-3 text-[length:var(--text-sm)] text-white"
          onClick={() => onSave(title, note)}
        >
          Save
        </button>
        <button
          type="button"
          data-testid={ids.linkCardCancel}
          className="h-7 rounded-control px-3 text-[length:var(--text-sm)] text-ink-muted hover:text-ink"
          onClick={onCancel}
        >
          Cancel
        </button>
      </div>
    </div>
  );
}
