import { useState } from 'react';
import type {
  RetroImpactDto,
  RetroInsightColumnDto,
  RetroInsightsMode,
} from '@shiplean/core/api/retros';
import { ActionChip } from '../../../retroShared/ActionChip';
import { retrosTestIds as ids } from '../../testIds';
import { VerdictChip } from '../recurring/VerdictChip';
import { useTouchShell } from '../../../../touch/useTouchShell';
import { ImpactChart } from './ImpactChart';
import { shortColumnLabel } from './impactGeometry';
import { ImpactNumbers } from './ImpactNumbers';
import { LatestMentionCallout } from './LatestMentionCallout';

/** One closed action item: verdict, when it closed, the chart, the numbers and, for No effect, the latest mention. */
export function ImpactCard({
  impact,
  columns,
  raisedIn,
  mode,
}: {
  impact: RetroImpactDto;
  columns: readonly RetroInsightColumnDto[];
  raisedIn: string | undefined;
  mode: RetroInsightsMode;
}) {
  const { touch } = useTouchShell();
  const [selected, setSelected] = useState<number | null>(null);
  const { action, theme, verdict } = impact;
  const unit = mode === 'week' ? 'week' : 'sprint';
  const closedIn = impact.closedAt >= 0 ? columns[impact.closedAt]?.label : undefined;
  return (
    <article
      data-testid={ids.retroImpactCard}
      data-action-key={action.key}
      data-verdict={verdict}
      className="grid gap-2 rounded-panel border border-edge bg-panel p-3"
    >
      <div className="flex items-center gap-2">
        <VerdictChip verdict={verdict} mode={mode} />
        <span className="flex-1" />
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          {raisedIn ? `raised ${raisedIn} · ` : ''}
          {closedIn ? `done ${closedIn}` : `done before ${columns[0]?.label ?? 'this range'}`}
        </span>
      </div>
      <h3 className="m-0 text-[length:var(--text-md)] font-semibold">{action.title}</h3>
      <div className="flex flex-wrap items-center gap-1.5 text-[length:var(--text-sm)] touch:[&>a]:min-h-[var(--touch-target)]">
        <ActionChip action={action} />
        <span className="text-ink-muted">for</span>
        <b>{theme.name}</b>
      </div>
      <ImpactChart
        label={theme.name}
        actionKey={action.key}
        columnLabels={columns.map((c) => c.label)}
        counts={impact.counts}
        closedAt={impact.closedAt}
        beforeAvg={impact.beforeAvg}
        afterAvg={impact.afterAvg}
        unit={unit}
        selected={touch ? selected : null}
        onSelect={touch ? (i) => setSelected((now) => (now === i ? null : i)) : undefined}
      />
      {touch && selected !== null ? (
        <p
          data-testid={ids.retroImpactReadout}
          className="m-0 text-[length:var(--text-sm)] text-ink-muted"
        >
          {shortColumnLabel(columns[selected]?.label ?? '')}: {impact.counts[selected] ?? 0}{' '}
          {impact.counts[selected] === 1 ? 'mention' : 'mentions'}
        </p>
      ) : null}
      <ImpactNumbers
        beforeAvg={impact.beforeAvg}
        afterAvg={impact.afterAvg}
        changePct={impact.changePct}
        unit={unit}
      />
      {verdict === 'noEffect' && impact.latestMention ? (
        <LatestMentionCallout mention={impact.latestMention} />
      ) : null}
    </article>
  );
}
