import type { CardDto } from '@shiplean/core/api/views';
import { TypeIcon } from '../../../../components/TypeIcon';
import { boardTestIds as ids } from '../../testIds';

/** The identity block over a card's actions: type icon, key · state, and the title (K1 header slot). */
export function CardSheetHeader({ card }: { card: CardDto }) {
  return (
    <div data-testid={ids.cardSheetHeader} className="-mx-3 grid gap-0.5 border-b border-edge px-5 pb-2">
      <div className="flex items-center gap-1.5 text-[length:var(--text-sm)] text-ink-muted">
        <TypeIcon type={card.type.id} />
        <span>
          {card.key} · {card.state.name}
        </span>
      </div>
      <div className="text-[length:var(--text-md)] font-semibold text-ink">{card.title}</div>
    </div>
  );
}
