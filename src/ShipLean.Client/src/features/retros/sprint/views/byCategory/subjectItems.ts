import type { RetroItemRefDto, SprintRetroDto } from '@shiplean/core/api/retros';

/** The work items a point can be about: the coverage rows plus the items on the cards, each once, in that order. */
export function subjectItems(retro: SprintRetroDto): RetroItemRefDto[] {
  const seen = new Set<string>();
  const items: RetroItemRefDto[] = [];
  const candidates = [
    ...retro.coverage.map((row) => row.item),
    ...retro.cards.flatMap((card) => (card.workItem ? [card.workItem] : [])),
  ];
  for (const item of candidates) {
    if (!seen.has(item.id)) {
      seen.add(item.id);
      items.push(item);
    }
  }
  return items;
}
