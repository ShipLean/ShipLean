import { useRef } from 'react';
import { attachmentTestIds as ids } from '../testIds';

// Replace…: opens the file picker. The file goes up as soon as it is chosen.
export function ReplaceButton({
  pending,
  onPick,
}: {
  pending: boolean;
  onPick: (file: File) => void;
}) {
  const picker = useRef<HTMLInputElement>(null);
  return (
    <>
      <button
        type="button"
        data-testid={ids.replace}
        disabled={pending}
        className="h-7 rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] disabled:opacity-50"
        onClick={() => picker.current?.click()}
      >
        {pending ? 'Replacing…' : 'Replace…'}
      </button>
      <input
        ref={picker}
        data-testid={ids.replaceInput}
        type="file"
        accept=".md,text/markdown"
        aria-label="Replace file"
        className="hidden"
        onChange={(event) => {
          const file = event.target.files?.[0];
          event.target.value = '';
          if (file) onPick(file);
        }}
      />
    </>
  );
}
