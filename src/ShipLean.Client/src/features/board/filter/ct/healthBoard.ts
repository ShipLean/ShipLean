import type { BoardViewDto } from '@shiplean/core/api/views';
import { featuresBoard } from '../../ct/fixtures';

const HEALTH_OF: Record<string, 'onTrack' | 'atRisk' | 'blocked' | null> = {
  'SHIP-104': 'atRisk',
  'SHIP-102': 'onTrack',
  'SHIP-90': null,
};

/** The Features board with a health on each Feature: SHIP-104 at risk, SHIP-102 on track, SHIP-90 none set. */
export function featuresWithHealth(): BoardViewDto {
  const base = featuresBoard();
  return {
    ...base,
    lanes: base.lanes.map((lane) => ({
      ...lane,
      cells: lane.cells.map((cell) => ({
        ...cell,
        cards: cell.cards.map((card) => ({ ...card, health: HEALTH_OF[card.key] ?? null })),
      })),
    })),
  };
}

/** What the server answers for ?health=…: the Features of other health leave the cells, the counts stay and the hidden
 * cards are counted on their column. */
export function filteredByHealth(base: BoardViewDto, search: URLSearchParams): BoardViewDto {
  const wanted = search.get('health');
  if (wanted === null) return base;
  const shows = (health: string | null) =>
    wanted === 'none' ? health === null : health === wanted;
  let hidden = 0;
  const lanes = base.lanes.map((lane) => ({
    ...lane,
    cells: lane.cells.map((cell) => {
      const cards = cell.cards.filter((card) => shows(card.health));
      hidden += cell.cards.length - cards.length;
      return { ...cell, cards };
    }),
  }));
  return {
    ...base,
    lanes,
    columns: base.columns.map((column, index) =>
      index === 0 ? { ...column, hiddenCount: hidden } : column,
    ),
  };
}
