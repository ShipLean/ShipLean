import { Link } from 'react-router-dom';
import { routePath } from '../../../routes';
import { retrosTestIds as ids } from '../testIds';
import type { SprintRetroSlotProps } from './SprintRetroSlotProps';

/** The retro header's four KPIs: finished items with a retro, recurring themes (linked to Insights), points to decide and action items raised. */
export function SprintRetroHeader({ project, retro }: SprintRetroSlotProps) {
  const { kpis } = retro;
  return (
    <section
      data-testid={ids.retroHeader}
      aria-label="Retro summary"
      className="flex flex-wrap items-end justify-end gap-6"
    >
      <div>
        <div
          data-testid={ids.retroKpiWithRetro}
          className="text-[20px] leading-tight font-bold tabular-nums"
        >
          {kpis.withRetro}{' '}
          <small className="text-[length:var(--text-sm)] font-medium text-ink-muted">
            / {kpis.finished}
          </small>
        </div>
        <div className="text-[length:var(--text-xs)] text-ink-muted">
          Finished items with a retro
        </div>
      </div>
      <div>
        <div
          data-testid={ids.retroKpiRecurring}
          className="text-[20px] leading-tight font-bold tabular-nums"
        >
          {kpis.recurringThemes}
        </div>
        <div className="text-[length:var(--text-xs)] text-ink-muted">
          Recurring themes ·{' '}
          <Link
            className="text-accent underline"
            to={routePath('retroInsights', { key: project.key })}
          >
            insights
          </Link>
        </div>
      </div>
      <div>
        <div
          data-testid={ids.retroKpiToDecide}
          className="text-[20px] leading-tight font-bold tabular-nums"
        >
          {kpis.pointsToDecide}
        </div>
        <div className="text-[length:var(--text-xs)] text-ink-muted">Points to decide</div>
      </div>
      <div>
        <div
          data-testid={ids.retroKpiActions}
          className="text-[20px] leading-tight font-bold tabular-nums"
        >
          {kpis.actionItemsRaised}{' '}
          <small className="text-[length:var(--text-sm)] font-medium text-ink-muted">new</small>
        </div>
        <div className="text-[length:var(--text-xs)] text-ink-muted">Action items raised</div>
      </div>
    </section>
  );
}
