import { useSearchParams } from 'react-router-dom';
import type { ProjectDto } from '../../../api/projects';
import { RetroActionDialogProvider } from '../../retroShared/actionDialog/RetroActionDialogProvider';
import { pagesTestIds } from '../../pages/testIds';
import { RetroQueryState } from '../RetroQueryState';
import { retrosTestIds as ids } from '../testIds';
import { useRetroInsights } from '../useRetroQueries';
import { ActionItemsTable } from './ActionItemsTable';
import { ChangesImpact } from './ChangesImpact';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';
import { KeepsWorking } from './KeepsWorking';
import { MergeThemes } from './MergeThemes';
import { RangeSelect, rangeOf } from './RangeSelect';
import { RecurringThemes } from './RecurringThemes';

const VIEWS = [
  { id: 'themes', label: 'Recurring themes' },
  { id: 'actions', label: 'Action items' },
  { id: 'impact', label: 'Changes & impact' },
] as const;
type InsightView = (typeof VIEWS)[number]['id'];

const viewOf = (params: URLSearchParams): InsightView =>
  VIEWS.find((v) => v.id === params.get('view'))?.id ?? 'themes';
function InsightBody({ view, ...props }: RetroInsightsSlotProps & { view: InsightView }) {
  if (view === 'actions') return <ActionItemsTable {...props} />;
  if (view === 'impact') {
    return (
      <div data-testid={pagesTestIds.slotRetroChangesImpact}>
        <ChangesImpact {...props} />
      </div>
    );
  }
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
  const range = rangeOf(params.get('range'));
  const query = useRetroInsights(project.id, range);
  const set = (name: 'view' | 'range', value: string) => {
    const next = new URLSearchParams(params);
    next.set(name, value);
    setParams(next, { replace: true });
  };
  return (
    <RetroActionDialogProvider projectId={project.id}>
      <div data-testid={ids.retroInsightsPanel} className="grid grid-cols-[minmax(0,1fr)] gap-4">
        <div className="sticky top-0 z-10 flex items-center gap-3 bg-canvas py-1">
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
          <RangeSelect range={range} onChange={(next) => set('range', next)} />
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
    </RetroActionDialogProvider>
  );
}
