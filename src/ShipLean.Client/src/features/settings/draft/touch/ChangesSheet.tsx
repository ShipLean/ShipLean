import { projectSettingsTabLabels } from '../../../../routes';
import { Sheet } from '../../../../touch/Sheet';
import { settingsTestIds } from '../../testIds';
import type { Change } from '../model';

/**
 * The unsaved changes on a phone: one row each, the pane's name in a column of its own and then the change (SHIP-1809
 * P3), and a tap closes the sheet and jumps to that change's tab.
 */
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
      <ul
        data-testid={settingsTestIds.changesList}
        className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-4 gap-y-1"
      >
        {changes.map((change) => (
          <li key={`${change.tab}-${change.label}`} className="col-span-2 grid grid-cols-subgrid">
            <button
              type="button"
              data-testid={settingsTestIds.changeItem}
              className="col-span-2 grid min-h-[var(--touch-target)] grid-cols-subgrid items-center rounded-control px-2 text-left"
              onClick={() => {
                onClose();
                onJump(change);
              }}
            >
              <span
                data-testid={settingsTestIds.changePane}
                className="text-[length:var(--text-sm)] whitespace-nowrap text-ink-muted"
              >
                {projectSettingsTabLabels[change.tab]}
              </span>
              <span className="text-[length:var(--text-base)] break-words text-ink">
                {change.label}
              </span>
            </button>
          </li>
        ))}
      </ul>
    </Sheet>
  );
}
