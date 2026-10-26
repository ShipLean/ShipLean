import type { CardDto } from '../../../api/views';
import { boardTestIds as ids } from '../testIds';

// Every child below the card is closed and the card is not in Done: it offers the move, and nothing moves on its own.
export function CardOffer({ card, onMoveToDone }: { card: CardDto; onMoveToDone?: () => void }) {
  const closed = card.rollups.reduce((sum, rollup) => sum + rollup.closed, 0);
  return (
    <div
      data-testid={ids.cardOffer}
      className="flex items-center gap-1.5 rounded-control py-1 pr-1 pl-2 text-[length:var(--text-xs)]"
      style={{ background: 'color-mix(in srgb, var(--color-ok) 12%, transparent)' }}
    >
      All {closed} closed
      <button
        type="button"
        data-testid={ids.cardMoveToDone}
        className="ml-auto rounded-control border border-edge bg-panel px-2 py-0.5 text-[length:var(--text-xs)] text-ink"
        onClick={onMoveToDone}
      >
        Move to Done
      </button>
    </div>
  );
}
