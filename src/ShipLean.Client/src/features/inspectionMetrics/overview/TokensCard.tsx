import type { InspectionMetricsViewDto } from '@shiplean/core/api/views';
import { inspectionMetricsTestIds as ids } from '../testIds';
import { ChangeChip } from './LeadTimeCard';
import { OVERLINE } from './overviewStyles';

const CARD =
  'panel grid min-w-[360px] content-start gap-2 px-4 py-3 text-left hover:border-signal focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-signal';

/**
 * The Overview's cost card (design ?view=overview, "Cost · tokens per task"): the last finished sprint's mean tokens per
 * task with the change against the sprint before, a bar per sprint, and this quarter and last. Hidden when agent runs are
 * off (cost null); when the store cannot total tokens per task it shows "—" with the server's note. Opens ?view=cost.
 */
export function TokensCard({ view, onOpen }: { view: InspectionMetricsViewDto; onOpen(): void }) {
  const cost = view.cost;
  if (cost === null) return null;
  const { finished, quarterToDate, lastQuarter } = view.headline;
  const last = cost.headline.finished;
  if (!cost.available || finished === null || last === null) {
    return (
      <button type="button" data-testid={ids.overviewTokensCard} onClick={onOpen} className={CARD}>
        <span className={OVERLINE}>Cost · tokens per task</span>
        <b data-testid={ids.overviewTokensCardValue} className="text-[28px] leading-none font-bold">
          —
        </b>
        <span data-testid={ids.overviewTokensEmpty} className="text-ink-muted">
          {cost.note ?? 'No finished sprint yet.'}
        </span>
      </button>
    );
  }
  const change = last.changePctVsPrevious;
  const max = Math.max(1, ...cost.periods.map((p) => p.stats.tokensPerTask ?? 0));
  return (
    <button type="button" data-testid={ids.overviewTokensCard} onClick={onOpen} className={CARD}>
      <div className="flex items-center gap-2">
        <span className={OVERLINE}>Cost · tokens per task</span>
        <span className="flex-1" />
        {change === null ? null : (
          <ChangeChip change={change} testId={ids.overviewTokensCardChange} />
        )}
      </div>
      <div className="flex items-baseline gap-2">
        <b data-testid={ids.overviewTokensCardValue} className="text-[28px] leading-none font-bold">
          {last.tokensPerTaskLabel}
        </b>
        <span className="text-ink-muted">
          mean, {finished.name} · {last.tasksWithRuns} {last.tasksWithRuns === 1 ? 'task' : 'tasks'}{' '}
          with agent runs
        </span>
      </div>
      <div aria-hidden="true" className="flex h-10 items-end gap-1">
        {view.periods.map((period) => {
          const stats = cost.periods.find((p) => p.periodId === period.id)?.stats;
          return (
            <span
              key={period.id}
              className="flex-1 rounded-t-[2px]"
              style={{
                height: `${Math.max(4, ((stats?.tokensPerTask ?? 0) / max) * 100)}%`,
                background: 'var(--type-feature)',
                opacity: period.id === finished.periodId ? 0.9 : period.inProgress ? 0.25 : 0.5,
              }}
            />
          );
        })}
      </div>
      <div className="flex flex-wrap gap-x-4 text-[length:var(--text-sm)] text-ink-muted">
        {quarterToDate !== null && cost.headline.quarterToDate !== null && (
          <span>
            {quarterToDate.quarter} to date{' '}
            <b className="text-ink">{cost.headline.quarterToDate.tokensPerTaskLabel}</b>
          </span>
        )}
        {lastQuarter !== null && cost.headline.lastQuarter !== null && (
          <span>
            {lastQuarter.quarter}{' '}
            <b className="text-ink">{cost.headline.lastQuarter.tokensPerTaskLabel}</b>
          </span>
        )}
        <span>Tokens only, no money yet</span>
      </div>
    </button>
  );
}
