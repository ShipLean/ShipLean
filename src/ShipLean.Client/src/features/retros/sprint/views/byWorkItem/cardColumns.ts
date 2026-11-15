import { RETRO_CATEGORIES } from '@shiplean/core/api/retros';
import type { AddTarget } from '../../../../retroShared/board/AddTarget';
import type {
  RetroCardDto,
  RetroCategory,
  RetroCategoryColumnDto,
  RetroPointDto,
} from '@shiplean/core/api/retros';

/** One retro's points as board columns in K3's order: one unthemed group per column, `toDecide` counted from the points. */
export function cardColumns(points: RetroPointDto[]): RetroCategoryColumnDto[] {
  return RETRO_CATEGORIES.map((category) => {
    const inColumn = points.filter((point: RetroPointDto) => point.category === category);
    return {
      category,
      groups: [{ theme: null, recurring: false, points: inColumn }],
      toDecide: inColumn.filter((point) => point.decision === 'undecided').length,
    };
  });
}

/** Where Add on a board writes: its work item, or the page's sprint when the retros are the sprint's own. */
export function cardAddTarget(card: RetroCardDto, sprintId: string): AddTarget {
  if (card.workItem) return { kind: 'item', workItemId: card.workItem.id };
  return {
    kind: 'sprint',
    sprintId,
    about: [{ value: '', label: 'The sprint' }],
  };
}

/** How many points a category has in an item's retros. */
export function categoryCounts(
  points: RetroPointDto[],
): { category: RetroCategory; count: number }[] {
  return RETRO_CATEGORIES.map((category) => ({
    category,
    count: points.filter((point) => point.category === category).length,
  }));
}
