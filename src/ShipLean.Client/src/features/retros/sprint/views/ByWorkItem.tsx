import { useState } from 'react';
import type { RetroPointDto } from '../../../../api/retros';
import type { SprintRetroSlotProps } from '../SprintRetroSlotProps';
import { ItemBoard } from './byWorkItem/ItemBoard';
import { filterPoints, matchesWrittenBy, type WrittenBy } from './byCategory/writtenBy';
import { WrittenByFilter } from './byCategory/WrittenByFilter';
import { retrosTestIds as ids } from '../../testIds';

/** The By work item view: one board per retro's item, a Feature's first, each with its raw failure log below. */
export function ByWorkItem({
  retro,
  decide = false,
  point,
  writtenBy: controlled,
  onAdded,
}: SprintRetroSlotProps & {
  writtenBy?: WrittenBy;
  /** Called with a point just added, so the tab can clear a filter that would hide it. */
  onAdded?: (point: RetroPointDto) => void;
}) {
  const [own, setOwn] = useState<WrittenBy>('all');
  const writtenBy = controlled ?? own;
  const shown = retro.cards.filter(
    (card) => writtenBy === 'all' || filterPoints(card.points, writtenBy).length > 0,
  );
  const isFeature = (card: (typeof shown)[number]) => card.workItem?.type.id === 'feature';
  const cards = [...shown.filter(isFeature), ...shown.filter((card) => !isFeature(card))];
  const filter = (p: RetroPointDto) =>
    matchesWrittenBy(p, writtenBy) && (!decide || p.decision === 'undecided');
  return (
    <div className="grid gap-3">
      {controlled === undefined ? <WrittenByFilter value={writtenBy} onChange={setOwn} /> : null}
      <div data-testid={ids.retroCardList} className="grid gap-3">
        {cards.length > 0 ? (
          cards.map((card) => (
            <ItemBoard
              key={card.id}
              card={card}
              filter={filter}
              highlightedPointId={point}
              onAdded={onAdded}
            />
          ))
        ) : (
          <span className="text-[length:var(--text-sm)] text-ink-muted">Nothing here.</span>
        )}
      </div>
    </div>
  );
}
