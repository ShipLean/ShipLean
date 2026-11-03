import { useState } from 'react';
import { retroSharedTestIds as ids } from '../testIds';

export function WhyEditor({
  saving,
  onSave,
  onCancel,
}: {
  saving: boolean;
  onSave: (reason: string) => void;
  onCancel: () => void;
}) {
  const [reason, setReason] = useState('');
  const text = reason.trim();
  return (
    <div className="grid gap-1">
      <textarea
        data-testid={ids.retroNoteWhyText}
        rows={2}
        value={reason}
        placeholder="Why isn't this needed? (required)"
        className="w-full resize-y rounded-[var(--radius-xs)] border border-edge bg-white px-1.5 py-1 text-[length:var(--text-xs)] text-ink"
        onChange={(event) => setReason(event.target.value)}
      />
      <div className="flex gap-1.5">
        <button
          type="button"
          data-testid={ids.retroNoteWhySave}
          disabled={text === '' || saving}
          className="h-[22px] cursor-pointer rounded-[var(--radius-xs)] border border-signal bg-signal px-2 text-[length:var(--text-xs)] font-semibold text-signal-ink disabled:cursor-default disabled:opacity-60"
          onClick={() => onSave(text)}
        >
          Save
        </button>
        <button
          type="button"
          data-testid={ids.retroNoteWhyCancel}
          className="h-[22px] cursor-pointer rounded-[var(--radius-xs)] border border-edge bg-white px-2 text-[length:var(--text-xs)] text-ink"
          onClick={onCancel}
        >
          Cancel
        </button>
      </div>
    </div>
  );
}
