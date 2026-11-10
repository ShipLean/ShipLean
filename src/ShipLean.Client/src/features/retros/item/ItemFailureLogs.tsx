import type { RetroItemDto } from '@shiplean/core/api/retros';
import { FailureLog } from '../sprint/views/byWorkItem/FailureLog';
import { retrosTestIds as ids } from '../testIds';
import type { RetroItemAddress } from './retroItemAddress';

/** The raw failure log of each retro shown, below the board: collapsed, read-only. A retro with an empty log adds nothing, and nothing is drawn when none has one. */
export function ItemFailureLogs({
  item,
  address,
}: {
  item: RetroItemDto;
  address: RetroItemAddress;
}) {
  const cards = item.cards.filter(
    (card) =>
      (address.retro === undefined || card.id === address.retro) &&
      Object.values(card.failureLog).some((lines) => lines.length > 0),
  );
  if (cards.length === 0) return null;
  return (
    <div data-testid={ids.retroItemLogs} className="grid gap-2">
      {cards.map((card) => (
        <div key={card.id} className="grid gap-1">
          {item.cards.length > 1 ? (
            <span className="text-[length:var(--text-xs)] text-ink-muted">
              {card.author.agentName ?? card.author.user.name}
            </span>
          ) : null}
          <FailureLog log={card.failureLog} />
        </div>
      ))}
    </div>
  );
}
