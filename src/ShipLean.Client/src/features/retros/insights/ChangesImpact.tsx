import type { RetroInsightsSlotProps } from './InsightsSlotProps';
import { ImpactCard } from './impact/ImpactCard';

/** Changes and impact: a card per closed action item with its mentions before and after. */
export function ChangesImpact({ insights }: RetroInsightsSlotProps) {
  const { impact, sprints } = insights;
  return (
    <div className="flex flex-col gap-3">
      <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
        For each closed action item: how often its theme came up before and after it shipped. Bars
        are mentions per sprint; the lines are the averages before and after. The verdict is worked
        out automatically: after at most half of before is <i>Helped</i>, lower is{' '}
        <i>Some effect</i>, the same or higher is <i>No effect</i>, and no full sprint since is{' '}
        <i>Too early</i>.
      </p>
      {impact.length === 0 ? (
        <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">Nothing here.</p>
      ) : (
        <div className="grid grid-cols-[repeat(auto-fill,minmax(340px,1fr))] gap-3">
          {impact.map((card) => (
            <ImpactCard
              key={card.action.workItemId}
              impact={card}
              sprints={sprints}
              raisedIn={
                insights.actionItems.find((r) => r.action.workItemId === card.action.workItemId)
                  ?.raisedIn?.name
              }
            />
          ))}
        </div>
      )}
    </div>
  );
}
