import type { RetroImpactDto } from '../../../../api/retros';
import type { SprintRef } from '../../../../api/refs';
import { ActionChip } from '../../../retroShared/ActionChip';
import { retrosTestIds as ids } from '../../testIds';
import { VerdictChip } from '../recurring/VerdictChip';
import { ImpactChart } from './ImpactChart';
import { ImpactNumbers } from './ImpactNumbers';
import { LatestMentionCallout } from './LatestMentionCallout';

/** One closed action item: verdict, when it closed, the chart, the numbers and, for No effect, the latest mention. */
export function ImpactCard({
  impact,
  sprints,
  raisedIn,
}: {
  impact: RetroImpactDto;
  sprints: readonly SprintRef[];
  raisedIn: string | undefined;
}) {
  const { action, theme, verdict } = impact;
  const closedIn = impact.closedAt >= 0 ? sprints[impact.closedAt]?.name : undefined;
  return (
    <article
      data-testid={ids.retroImpactCard}
      data-action-key={action.key}
      data-verdict={verdict}
      className="grid gap-2 rounded-panel border border-edge bg-panel p-3"
    >
      <div className="flex items-center gap-2">
        <VerdictChip verdict={verdict} />
        <span className="flex-1" />
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          {raisedIn ? `raised ${raisedIn} · ` : ''}
          {closedIn ? `done ${closedIn}` : `done before ${sprints[0]?.name ?? 'this range'}`}
        </span>
      </div>
      <h3 className="m-0 text-[length:var(--text-md)] font-semibold">{action.title}</h3>
      <div className="flex flex-wrap items-center gap-1.5 text-[length:var(--text-sm)]">
        <ActionChip action={action} />
        <span className="text-ink-muted">for</span>
        <b>{theme.name}</b>
      </div>
      <ImpactChart
        label={theme.name}
        actionKey={action.key}
        sprintNames={sprints.map((s) => s.name)}
        counts={impact.counts}
        closedAt={impact.closedAt}
        beforeAvg={impact.beforeAvg}
        afterAvg={impact.afterAvg}
      />
      <ImpactNumbers
        beforeAvg={impact.beforeAvg}
        afterAvg={impact.afterAvg}
        changePct={impact.changePct}
      />
      {verdict === 'noEffect' && impact.latestMention ? (
        <LatestMentionCallout mention={impact.latestMention} />
      ) : null}
    </article>
  );
}
