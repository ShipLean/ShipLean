import { Sheet } from '../../../../touch/Sheet';
import { settingsTestIds } from '../../testIds';
import type { Change } from '../model';

/** The unsaved changes on a phone: one row each, and a tap closes the sheet and jumps to that change's tab. */
export function ChangesSheet({
  open,
  changes,
  onJump,
  onClose,
}: {
  open: boolean;
  changes: Change[];
  onJump(change: Change): void;
  onClose(): void;
}) {
  return (
    <Sheet open={open} title="Unsaved changes" onClose={onClose}>
      <ul data-testid={settingsTestIds.changesList} className="grid gap-1">
        {changes.map((change) => (
          <li key={`${change.tab}-${change.label}`}>
            <button
              type="button"
              data-testid={settingsTestIds.changeItem}
              className="min-h-[var(--touch-target)] w-full rounded-control px-2 text-left text-[length:var(--text-base)] text-ink"
              onClick={() => {
                onClose();
                onJump(change);
              }}
            >
              {change.label}
            </button>
          </li>
        ))}
      </ul>
    </Sheet>
  );
}
