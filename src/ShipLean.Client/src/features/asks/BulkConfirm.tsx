import { showToast } from '../../components/toastStore';
import type { AsksRegionProps } from './regions';
import { asksTestIds as ids } from './testIds';
import { useAskWrites } from './useAskWrites';

// "✓ Confirm N recommended" (UI4): only when two or more decisions are open. It sends the open numbers on screen.
export function BulkConfirm({ item, asks }: AsksRegionProps) {
  const writes = useAskWrites(item.projectId, item.key, item.id);
  const open = asks.decisions.filter((decision) => decision.state === 'open');
  if (open.length < 2) return null;
  const numbers = open.map((decision) => decision.number);
  return (
    <button
      type="button"
      data-testid={ids.bulkConfirm}
      title="Confirm every decision that's going with its recommended answer"
      disabled={writes.confirm.isPending}
      className="inline-flex h-8 items-center rounded-control border border-edge bg-panel px-2.5 text-[length:var(--text-sm)] font-medium text-ink hover:bg-signal-soft disabled:opacity-45"
      onClick={() =>
        writes.confirm.mutate(numbers, {
          onSuccess: () =>
            showToast(`${numbers.length} decisions confirmed as recommended`, { tone: 'ok' }),
          onError: (error) => showToast(error.message, { tone: 'danger' }),
        })
      }
    >
      ✓ Confirm {numbers.length} recommended
    </button>
  );
}
