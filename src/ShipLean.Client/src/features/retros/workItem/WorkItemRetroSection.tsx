import { RetroCard } from '../../retroShared/RetroCard';
import { RetroQueryState } from '../RetroQueryState';
import { retrosTestIds as ids } from '../testIds';
import { useWorkItemRetros } from '../useRetroQueries';

// The item's retrospectives, newest first, as the sprint tab's By work item view shows them but read-only: points can be
// read here and changed on the Retro tab. Nothing shows while the item has none.
export function WorkItemRetroSection({
  projectId,
  workItemId,
}: {
  projectId: string;
  workItemId: string;
}) {
  const query = useWorkItemRetros(projectId, workItemId);
  return (
    <RetroQueryState query={query}>
      {({ cards }) =>
        cards.length === 0 ? null : (
          <section
            data-testid={ids.workItemRetroSection}
            aria-label="Retrospective"
            className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-2"
          >
            <h2 className="text-[length:var(--text-md)] font-semibold">Retrospective</h2>
            {cards.map((card) => (
              <RetroCard key={card.id} card={card} showActions={false} />
            ))}
          </section>
        )
      }
    </RetroQueryState>
  );
}
