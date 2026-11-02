import { backlogTestIds as ids } from '../testIds';
import { useIsFixed } from './queueFixed';

// The mark a fixed row wears in an open queue; nothing outside one.
export function QueueDoneMark({ rowId }: { rowId: string }) {
  if (!useIsFixed(rowId)) return null;
  return (
    <span data-testid={ids.queueDone} role="img" aria-label="Fixed" className="font-bold text-good">
      ✓
    </span>
  );
}
