import type { LinkCardDto } from '../../../../api/linkCards';
import { featureTestIds as ids } from '../../testIds';
import { LinkCard } from './LinkCard';

// The cards of one group (Code or Pages), two across.
export function LinkGroup({
  group,
  title,
  cards,
  projectId,
  itemKey,
}: {
  group: 'code' | 'page';
  title: string;
  cards: LinkCardDto[];
  projectId: string;
  itemKey: string;
}) {
  if (cards.length === 0) return null;
  return (
    <div data-testid={ids.linkGroup} data-group={group} className="grid gap-1">
      <h3 className="m-0 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
        {title}
      </h3>
      <ul className="m-0 grid list-none grid-cols-2 gap-2 p-0">
        {cards.map((card) => (
          <LinkCard key={card.id} card={card} projectId={projectId} itemKey={itemKey} />
        ))}
      </ul>
    </div>
  );
}
