import { useState } from 'react';
import { RetroCard } from '../../../retroShared/RetroCard';
import { retrosTestIds as ids } from '../../testIds';
import type { SprintRetroSlotProps } from '../SprintRetroSlotProps';
import { filterPoints, type WrittenBy } from './byCategory/writtenBy';
import { WrittenByFilter } from './byCategory/WrittenByFilter';

/** The By work item view: one card per retro, feature cards first, the raw failure log collapsed. */
export function ByWorkItem({ retro }: SprintRetroSlotProps) {
  const [writtenBy, setWrittenBy] = useState<WrittenBy>('all');
  const shown = retro.cards
    .map((card) => ({ ...card, points: filterPoints(card.points, writtenBy) }))
    .filter((card) => writtenBy === 'all' || card.points.length > 0);
  const isFeature = (card: (typeof shown)[number]) => card.workItem?.type.id === 'feature';
  const cards = [...shown.filter(isFeature), ...shown.filter((card) => !isFeature(card))];
  return (
    <div className="grid gap-3">
      <WrittenByFilter value={writtenBy} onChange={setWrittenBy} />
      <div data-testid={ids.retroCardList} className="grid gap-3">
        {cards.length > 0 ? (
          cards.map((card) => <RetroCard key={card.id} card={card} showActions={false} />)
        ) : (
          <span className="text-[length:var(--text-sm)] text-ink-muted">Nothing here.</span>
        )}
      </div>
    </div>
  );
}
