import { useState } from 'react';
import type { RetroCategory, RetroPointDto } from '@shiplean/core/api/retros';
import { RetroBoard } from '../../../retroShared/board/RetroBoard';
import { CategoryTiles } from '../CategoryTiles';
import type { SprintRetroSlotProps } from '../SprintRetroSlotProps';
import { FeatureRetroCallout } from './byCategory/FeatureRetroCallout';
import { subjectItems } from './byCategory/subjectItems';
import { matchesWrittenBy, type WrittenBy } from './byCategory/writtenBy';
import { WrittenByFilter } from './byCategory/WrittenByFilter';

/**
 * The By category view: the Feature retro callout over the shared board, whose Add on a column writes to the sprint or
 * to one of its items. Given `category` and `onCategory` (the touch page), the category tiles pick the one column shown.
 */
export function ByCategory({
  sprint,
  retro,
  decide = false,
  point,
  writtenBy: controlled,
  onAdded,
  category,
  onCategory,
  onRecurring,
}: SprintRetroSlotProps & {
  writtenBy?: WrittenBy;
  /** Called with a point just added, so the tab can clear a filter that would hide it. */
  onAdded?: (point: RetroPointDto) => void;
  /** At touch, the one category on screen, picked with the tiles. */
  category?: RetroCategory;
  onCategory?: (category: RetroCategory) => void;
  /** At touch, opens the Recurring sheet from a theme heading. */
  onRecurring?: () => void;
}) {
  // The panel's toolbar owns the filter; on its own the view shows the filter itself.
  const [own, setOwn] = useState<WrittenBy>('all');
  const writtenBy = controlled ?? own;
  const featureCard = retro.cards.find((card) => card.workItem?.type.id === 'feature');
  const about = [
    { value: '', label: 'The sprint' },
    ...subjectItems(retro).map((item) => ({ value: item.id, label: `${item.key} ${item.title}` })),
  ];
  const filter = (p: RetroPointDto) =>
    matchesWrittenBy(p, writtenBy) && (!decide || p.decision === 'undecided');
  const picking = category !== undefined && onCategory !== undefined;
  const columns = picking
    ? retro.categories.filter((c) => c.category === category)
    : retro.categories;
  return (
    <div className="grid gap-3">
      {controlled === undefined ? <WrittenByFilter value={writtenBy} onChange={setOwn} /> : null}
      {featureCard ? <FeatureRetroCallout card={featureCard} /> : null}
      {picking ? (
        <CategoryTiles
          columns={retro.categories}
          filter={filter}
          picked={category}
          onPick={onCategory}
        />
      ) : null}
      <RetroBoard
        columns={columns}
        groupByTheme
        filter={filter}
        note={{ showItem: true, highlightedPointId: point }}
        add={{ kind: 'sprint', sprintId: sprint.id, about }}
        scope={{ kind: 'sprint', sprintId: sprint.id }}
        onAdded={onAdded}
        onRecurring={onRecurring}
      />
    </div>
  );
}
