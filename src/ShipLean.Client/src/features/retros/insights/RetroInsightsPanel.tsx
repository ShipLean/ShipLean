import { useSearchParams } from 'react-router-dom';
import type { ProjectDto } from '../../../api/projects';
import type { RetroRange } from '../../../api/retrosApi';
import { RetroQueryState } from '../RetroQueryState';
import { retrosTestIds as ids } from '../testIds';
import { useRetroInsights } from '../useRetroQueries';
import { ActionItemsTable } from './ActionItemsTable';
import { ChangesImpact } from './ChangesImpact';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';
import { KeepsWorking } from './KeepsWorking';
import { MergeThemes } from './MergeThemes';
import { RecurringThemes } from './RecurringThemes';

const VIEWS = [
  { id: 'themes', label: 'Recurring themes' },
  { id: 'actions', label: 'Action items' },
  { id: 'impact', label: 'Changes & impact' },
] as const;
type InsightView = (typeof VIEWS)[number]['id'];

const RANGES = [
  { id: '6', label: 'Last 6 sprints' },
  { id: '3', label: 'Last 3 sprints' },
  { id: 'all', label: 'All sprints' },
] as const satisfies readonly { id: RetroRange; label: string }[];

const viewOf = (params: URLSearchParams): InsightView =>
  VIEWS.find((v) => v.id === params.get('view'))?.id ?? 'themes';
const rangeOf = (params: URLSearchParams): RetroRange =>
  RANGES.find((r) => r.id === params.get('range'))?.id ?? '6';

function InsightBody({ view, ...props }: RetroInsightsSlotProps & { view: InsightView }) {
  if (view === 'actions') return <ActionItemsTable {...props} />;
  if (view === 'impact') return <ChangesImpact {...props} />;
  return (
    <>
      <RecurringThemes {...props} />
      <KeepsWorking {...props} />
    </>
  );
}

/**
 * Retrospectives › Insights: the view tabs (opens on Recurring themes) and the range select, both kept in the address
 * (?view, ?range), then the chosen view's parts. The range is the query's parameter.
 */
export function RetroInsightsPanel({ project }: { project: ProjectDto }) {
  const [params, setParams] = useSearchParams();
  const view = viewOf(params);
  const range = rangeOf(params);
  const query = useRetroInsights(project.id, range);
  const set = (name: 'view' | 'range', value: string) => {
    const next = new URLSearchParams(params);
    next.set(name, value);
    setParams(next, { replace: true });
  };
  return (
    <div data-testid={ids.retroInsightsPanel} className="grid gap-4">
      <div className="flex items-center gap-3">
        <div
          role="group"
          aria-label="Insight"
          className="inline-flex overflow-hidden rounded-control border border-edge"
        >
          {VIEWS.map((v) => (
            <button
              key={v.id}
              type="button"
              data-testid={ids.retroInsightsTab}
              data-value={v.id}
              aria-pressed={view === v.id}
              className="cursor-pointer border-r border-edge bg-panel px-3 py-1 text-[length:var(--text-sm)] text-ink last:border-r-0 aria-pressed:bg-canvas aria-pressed:font-semibold"
              onClick={() => set('view', v.id)}
            >
              {v.label}
            </button>
          ))}
        </div>
        <select
          data-testid={ids.retroInsightsRange}
          aria-label="Range"
          value={range}
          className="rounded-control border border-edge bg-panel px-2 py-1 text-[length:var(--text-sm)]"
          onChange={(event) => set('range', event.target.value)}
        >
          {RANGES.map((r) => (
            <option key={r.id} value={r.id}>
              {r.label}
            </option>
          ))}
        </select>
        <span className="flex-1" />
        {query.data ? <MergeThemes project={project} insights={query.data} /> : null}
      </div>
      <RetroQueryState query={query}>
        {(insights) => {
          const props = { project, insights };
          return <InsightBody view={view} {...props} />;
        }}
      </RetroQueryState>
    </div>
  );
}
