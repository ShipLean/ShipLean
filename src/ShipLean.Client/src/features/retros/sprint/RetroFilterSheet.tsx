import { Sheet } from '../../../touch/Sheet';
import { retrosTestIds as ids } from '../testIds';
import { DecideFilter } from './DecideFilter';
import type { WrittenBy } from './views/byCategory/writtenBy';
import { WrittenByFilter } from './views/byCategory/WrittenByFilter';

/**
 * The Filter sheet at touch (D3): Written by and Points (All points | To decide), each applied as it is tapped, with
 * Clear (both back to All) and Show points (closes the sheet).
 */
export function RetroFilterSheet({
  open,
  writtenBy,
  decide,
  onWrittenBy,
  onDecide,
  onClear,
  onClose,
}: {
  open: boolean;
  writtenBy: WrittenBy;
  decide: boolean;
  onWrittenBy: (value: WrittenBy) => void;
  onDecide: (on: boolean) => void;
  onClear: () => void;
  onClose: () => void;
}) {
  return (
    <Sheet
      open={open}
      title="Filter"
      onClose={onClose}
      footer={
        <>
          <button
            type="button"
            data-testid={ids.retroFilterClear}
            className="btn-ghost"
            onClick={onClear}
          >
            Clear
          </button>
          <span className="flex-1" />
          <button
            type="button"
            data-testid={ids.retroFilterShow}
            className="btn-signal"
            onClick={onClose}
          >
            Show points
          </button>
        </>
      }
    >
      <div className="grid gap-4">
        <WrittenByFilter value={writtenBy} onChange={onWrittenBy} />
        <div className="flex items-center gap-2 text-[length:var(--text-sm)] text-ink-muted">
          <span>Points</span>
          <DecideFilter decide={decide} onChange={(value) => onDecide(value === 'open')} />
        </div>
        <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
          To decide hides Went well and every point that has an action item or is marked Not needed.
        </p>
      </div>
    </Sheet>
  );
}
