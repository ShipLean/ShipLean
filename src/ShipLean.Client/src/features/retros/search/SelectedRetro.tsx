import { RetroCard } from '../../retroShared/RetroCard';
import { retrosTestIds as ids } from '../testIds';
import { logMatches, pointMatches } from './parts/matches';
import type { RetrosSearchSlotProps } from './SearchSlotProps';

/** The whole retro of the selected point: its card with the point highlighted, and the raw log open when that is where the search matched. */
export function SelectedRetro({ query, search }: RetrosSearchSlotProps) {
  const card = search.selected;
  const highlighted = card ? search.rows.find((row) => row.retroId === card.id) : undefined;
  const inLog =
    card !== null &&
    logMatches(card, query.q) &&
    !(highlighted && pointMatches(highlighted, query.q));
  return (
    <aside data-testid={ids.retrosSelected} aria-label="Retrospective" className="min-w-0">
      {card ? (
        <RetroCard
          key={`${card.id}:${inLog}`}
          card={card}
          highlightPointId={highlighted?.id}
          logDefaultOpen={inLog}
          hideEmptyCategories
        />
      ) : (
        <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
          Select a point to see its whole retro.
        </p>
      )}
    </aside>
  );
}
