import { useState } from 'react';
import { retroSharedTestIds as ids } from '../testIds';

// TODO: the finished Why editor styling replaces this plain form.
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
        className="w-full rounded-sm border border-edge bg-canvas px-2 py-1 text-[length:var(--text-sm)] text-ink"
        onChange={(event) => setReason(event.target.value)}
      />
      <div className="flex gap-1.5">
        <button
          type="button"
          data-testid={ids.retroNoteWhySave}
          disabled={text === '' || saving}
          className="cursor-pointer rounded-control bg-accent px-2 py-0.5 text-[length:var(--text-xs)] text-signal-ink disabled:cursor-default disabled:opacity-60"
          onClick={() => onSave(text)}
        >
          Save
        </button>
        <button
          type="button"
          data-testid={ids.retroNoteWhyCancel}
          className="cursor-pointer px-2 py-0.5 text-[length:var(--text-xs)] text-ink"
          onClick={onCancel}
        >
          Cancel
        </button>
      </div>
    </div>
  );
}
