import { useState } from 'react';
import { Dialog } from '../../../components/Dialog';
import { backlogTestIds as ids } from '../testIds';
import { useListCommand } from './listCommands';

const KEYS: readonly { keys: readonly string[]; label: string }[] = [
  { keys: ['J', 'K'], label: 'Next, previous row' },
  { keys: ['X'], label: 'Select this row' },
  { keys: ['Esc'], label: 'Clear the selection, or close' },
  { keys: ['Enter'], label: 'Open the item' },
  { keys: ['0-9'], label: 'Start typing points' },
  { keys: ['N'], label: 'Add below this row' },
  { keys: ['M', '1'], label: 'Move to a sprint' },
  { keys: ['M', 'T'], label: 'Move to the top' },
  { keys: ['M', 'B'], label: 'Move to the bottom' },
  { keys: ['/'], label: 'Search this list' },
  { keys: ['?'], label: 'This list' },
];

// The keyboard shortcuts dialog. It registers the `help` command, which the `?` key and the toolbar button run.
export function KeysDialog() {
  const [open, setOpen] = useState(false);
  useListCommand('help', () => setOpen(true));
  if (!open) return null;
  return (
    <Dialog
      title="Keyboard, on the list"
      onClose={() => setOpen(false)}
      onBackdropClick={() => setOpen(false)}
      testId={ids.keysDialog}
      footer={
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          These only fire when the list has focus, never while you type in a field.
        </span>
      }
    >
      <dl className="m-0 grid gap-1.5">
        {KEYS.map(({ keys, label }) => (
          <div key={label} className="flex items-center justify-between gap-4">
            <dt className="text-ink">{label}</dt>
            <dd className="m-0 flex gap-1">
              {keys.map((key) => (
                <kbd
                  key={key}
                  className="rounded-control border border-edge bg-canvas px-1.5 font-mono text-[length:var(--text-xs)] text-ink-muted"
                >
                  {key}
                </kbd>
              ))}
            </dd>
          </div>
        ))}
      </dl>
    </Dialog>
  );
}
