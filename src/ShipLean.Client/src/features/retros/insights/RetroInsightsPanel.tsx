import { useSearchParams } from 'react-router-dom';
import { useTouchShell } from '../../../touch/useTouchShell';
import type { ProjectDto } from '@shiplean/core/api/projects';
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
import { InsightsTouchToolbar } from './touch/InsightsTouchToolbar';
import { DefinitionsSheet } from './touch/DefinitionsSheet';
import { RangeSheet } from './touch/RangeSheet';
import { readSheet, withSheet } from './touch/insightsAddress';

const VIEWS = [
  { id: 'themes', label: 'Recurring themes', short: 'Themes' },
  { id: 'actions', label: 'Action items', short: 'Action items' },
  { id: 'impact', label: 'Changes & impact', short: 'Impact' },
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
  const { touch } = useTouchShell();
  // The sheets the address names, read at touch only.
  const sheet = touch ? readSheet(params) : null;
  const closeSheet = () => setParams(withSheet(params, null), { replace: true });
  const pickRange = (next: string) => {
    const picked = new URLSearchParams(params);
    picked.set('range', next);
    picked.delete('sheet');
    setParams(picked, { replace: true });
  };
  const merge = query.data ? <MergeThemes project={project} /> : null;
  return (
    <RetroActionDialogProvider projectId={project.id}>
      <div data-testid={ids.retroInsightsPanel} className="grid grid-cols-[minmax(0,1fr)] gap-4">
        {touch ? (
          <InsightsTouchToolbar
            projectKey={project.key}
            views={VIEWS}
            view={view}
            range={range}
            unthemedTotal={query.data?.unthemed.total ?? 0}
            onView={(next) => set('view', next)}
            merge={merge}
          />
        ) : (
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
            {merge}
          </div>
        )}
        <RetroQueryState query={query}>
          {(insights) => {
            const props = { project, insights };
            return <InsightBody view={view} {...props} />;
          }}
        </RetroQueryState>
        {sheet?.kind === 'range' ? (
          <RangeSheet range={range} onPick={pickRange} onClose={closeSheet} />
        ) : null}
        {sheet?.kind === 'defs' ? (
          <DefinitionsSheet
            entry={sheet.entry}
            mode={query.data?.mode ?? 'sprint'}
            onClose={closeSheet}
          />
        ) : null}
      </div>
    </RetroActionDialogProvider>
  );
}
