import type { ReactNode } from 'react';
import type { InspectionCostDto, InspectionHeadlineDto } from '@shiplean/core/api/views';
import { ChangeChip } from '../overview/LeadTimeCard';
import { OVERLINE } from '../overview/overviewStyles';
import { inspectionMetricsTestIds as ids } from '../testIds';

function Card({
  testId,
  title,
  value,
  of,
  chip,
  children,
}: {
  testId: string;
  title: string;
  value: ReactNode;
  of: ReactNode;
  chip?: ReactNode;
  children?: ReactNode;
}) {
  return (
    <div data-testid={testId} className="panel grid content-start gap-2 px-4 py-3">
      <span className={OVERLINE}>{title}</span>
      <div className="flex flex-wrap items-baseline gap-2">
        <b
          data-testid={ids.costCardValue}
          className="text-[28px] leading-none font-bold tabular-nums"
        >
          {value}
        </b>
        <span className="text-ink-muted">{of}</span>
        {chip}
      </div>
      <div className="flex flex-wrap gap-x-4 text-[length:var(--text-sm)] text-ink-muted">
        {children}
      </div>
    </div>
  );
}

const prs = (n: number | null) =>
  n === null ? 'no linked PR merged' : `${n} ${n === 1 ? 'PR' : 'PRs'} merged`;

/**
 * The Cost view's three cards for the last finished sprint (never the running one): tokens per task with this quarter and
 * last, tokens per merged PR with the sprint's total, and the share of tasks with agent runs. Every number is the server's
 * label; the sprint and quarter names come from the lead-time headline.
 */
export function CostCards({
  cost,
  headline,
}: {
  cost: InspectionCostDto;
  headline: InspectionHeadlineDto;
}) {
  const last = cost.headline.finished;
  const { quarterToDate, lastQuarter } = cost.headline;
  const name = headline.finished?.name ?? 'no finished sprint yet';
  const change = last?.changePctVsPrevious ?? null;
  return (
    <div className="grid grid-cols-[repeat(auto-fit,minmax(240px,1fr))] gap-3">
      <Card
        testId={ids.costCardPerTask}
        title="Tokens per task"
        value={last?.tokensPerTaskLabel ?? '—'}
        of={`mean, ${name}`}
        chip={change === null ? null : <ChangeChip change={change} />}
      >
        {headline.quarterToDate !== null && quarterToDate !== null && (
          <span>
            {headline.quarterToDate.quarter} to date{' '}
            <b className="text-ink">{quarterToDate.tokensPerTaskLabel}</b>
          </span>
        )}
        {headline.lastQuarter !== null && lastQuarter !== null && (
          <span>
            {headline.lastQuarter.quarter}{' '}
            <b className="text-ink">{lastQuarter.tokensPerTaskLabel}</b>
          </span>
        )}
      </Card>
      <Card
        testId={ids.costCardPerMergedPr}
        title="Tokens per merged PR"
        value={last?.tokensPerMergedPrLabel ?? '—'}
        of={last === null ? name : `${prs(last.mergedPrs)} in ${name}`}
      >
        {last !== null && (
          <span>
            Total <b className="text-ink">{last.tokensTotalLabel}</b>
          </span>
        )}
      </Card>
      <Card
        testId={ids.costCardWithRuns}
        title="Tasks with agent runs"
        value={last?.tasksWithRunsPct == null ? '—' : `${last.tasksWithRunsPct}%`}
        of={last === null ? name : `${last.tasksWithRuns} of ${last.tasks} in ${name}`}
      >
        <span>The rest were done without an agent</span>
      </Card>
    </div>
  );
}
