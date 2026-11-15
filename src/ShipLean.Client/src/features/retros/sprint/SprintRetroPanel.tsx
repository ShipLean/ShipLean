import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import type { RetroPointDto, SprintRetroDto } from '@shiplean/core/api/retros';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { RetroActionDialogProvider } from '../../retroShared/actionDialog/RetroActionDialogProvider';
import { routePath } from '../../../routes';
import { MoreButton } from '../../../touch/MoreButton';
import { useTouchShell } from '../../../touch/useTouchShell';
import { pagesTestIds } from '../../pages/testIds';
import { RetroQueryState } from '../RetroQueryState';
import { retrosTestIds as ids } from '../testIds';
import { useSprintRetro } from '../useRetroQueries';
import { DecideFilter } from './DecideFilter';
import { RetroRail } from './rail/RetroRail';
import { RetroFilterSheet } from './RetroFilterSheet';
import { RetroNumbers } from './RetroNumbers';
import { RetroRailSheets, type RailSheet } from './RetroRailSheets';
import { SprintRetroHeader } from './SprintRetroHeader';
import { parseSprintRetroAddress, withSprintRetroValue } from './sprintRetroAddress';
import { ByCategory } from './views/ByCategory';
import { ByWorkItem } from './views/ByWorkItem';
import { matchesWrittenBy } from './views/byCategory/writtenBy';
import { WrittenByFilter } from './views/byCategory/WrittenByFilter';

const VIEWS = [
  { id: 'category', label: 'By category', testId: ids.retroViewByCategory },
  { id: 'item', label: 'By work item', testId: ids.retroViewByItem },
] as const;

/**
 * The sprint Retro tab: the header, the By category / By work item toggle (opens on By category), the chosen view and
 * the rail. The view, Written by, decide and point live in the address (sprintRetroAddress.ts), not in state. The data is the sprint's SprintRetroDto; each part is its own component and takes it whole.
 * At 900px and below (SHIP-727): the four numbers are buttons, the rail's sections open as sheets, the toolbar holds the
 * view, Filter (n) and ⋯ and doesn't stick, and the category tiles (kept in the address as `cat`) pick one column.
 */
export function SprintRetroPanel({ project, sprint }: { project: ProjectDto; sprint: SprintDto }) {
  const [params, setParams] = useSearchParams();
  const { view, writtenBy, decide, point, cat } = parseSprintRetroAddress(params);
  const query = useSprintRetro(project.id, sprint.id);
  const { touch } = useTouchShell();
  const navigate = useNavigate();
  const [sheet, setSheet] = useState<RailSheet | null>(null);
  const [filterOpen, setFilterOpen] = useState(false);
  // Turning on To decide while the picked category has nothing to decide (Went well never has) moves to the first
  // category that has points to decide (D7).
  const setDecide = (retro: SprintRetroDto, on: boolean) => {
    let next = withSprintRetroValue(params, 'decide', on ? 'open' : 'all');
    const shown = retro.categories.find((c) => c.category === cat);
    const first = retro.categories.find((c) => c.toDecide > 0);
    if (on && (shown?.toDecide ?? 0) === 0 && first)
      next = withSprintRetroValue(next, 'cat', first.category);
    setParams(next);
  };
  const filterCount = (writtenBy === 'all' ? 0 : 1) + (decide ? 1 : 0);
  const more = [
    { id: 'coverage', label: 'Coverage', onSelect: () => setSheet('coverage') },
    { id: 'actions', label: 'Action items', onSelect: () => setSheet('actions') },
    { id: 'recurring', label: 'Recurring themes', onSelect: () => setSheet('recurring') },
    {
      id: 'insights',
      label: 'Retrospective insights',
      onSelect: () => navigate(routePath('retroInsights', { key: project.key })),
    },
    {
      id: 'search',
      label: 'Search retrospectives',
      onSelect: () => navigate(routePath('retros', { key: project.key })),
    },
  ];
  // A point just added must stay visible: To decide hides Went well, Written by hides the other authors.
  const clearHidingFilters = (added: RetroPointDto) => {
    let next = params;
    if (decide && added.decision !== 'undecided')
      next = withSprintRetroValue(next, 'decide', 'all');
    if (!matchesWrittenBy(added, writtenBy)) next = withSprintRetroValue(next, 'writtenBy', 'all');
    if (next !== params) setParams(next);
  };
  return (
    <RetroActionDialogProvider projectId={project.id}>
      <RetroQueryState query={query}>
        {(retro) => {
          const props = { project, sprint, retro, decide, point };
          return (
            <div
              data-testid={ids.sprintRetroPanel}
              className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-4"
            >
              {touch ? (
                <RetroNumbers
                  kpis={retro.kpis}
                  decide={decide}
                  onOpen={setSheet}
                  onToggleDecide={() => setDecide(retro, !decide)}
                />
              ) : (
                <SprintRetroHeader {...props} />
              )}
              <div
                data-testid={ids.retroToolbar}
                className="sticky top-0 z-10 flex flex-wrap items-center gap-3 bg-canvas py-1 touch:static touch:flex-nowrap touch:gap-2"
              >
                <div
                  role="group"
                  aria-label="Retro view"
                  className="inline-flex overflow-hidden rounded-control border border-edge"
                >
                  {VIEWS.map((v) => (
                    <button
                      key={v.id}
                      type="button"
                      data-testid={v.testId}
                      aria-pressed={view === v.id}
                      className="cursor-pointer border-r border-edge bg-panel px-3 py-1 text-[length:var(--text-sm)] text-ink last:border-r-0 aria-pressed:bg-canvas aria-pressed:font-semibold touch:min-h-[var(--touch-target)] touch:px-2.5"
                      onClick={() => setParams(withSprintRetroValue(params, 'view', v.id))}
                    >
                      {v.label}
                    </button>
                  ))}
                </div>
                <span className="flex-1" />
                {touch ? (
                  <>
                    <button
                      type="button"
                      data-testid={ids.retroFilterOpen}
                      data-count={filterCount}
                      aria-haspopup="dialog"
                      className={`inline-flex min-h-[var(--touch-target)] flex-none cursor-pointer items-center gap-1.5 rounded-control border px-3 text-[length:var(--text-sm)] text-ink ${filterCount > 0 ? 'border-signal bg-signal-soft font-semibold' : 'border-edge bg-panel'}`}
                      onClick={() => setFilterOpen(true)}
                    >
                      Filter
                      {filterCount > 0 ? (
                        <span className="inline-grid h-[18px] min-w-[18px] place-items-center rounded-full bg-signal px-[5px] text-[length:var(--text-2xs)] text-signal-ink tabular-nums">
                          {filterCount}
                        </span>
                      ) : null}
                    </button>
                    <span className="flex-none rounded-control border border-edge bg-panel">
                      <MoreButton actions={more} />
                    </span>
                  </>
                ) : (
                  <>
                    <WrittenByFilter
                      value={writtenBy}
                      onChange={(value) =>
                        setParams(withSprintRetroValue(params, 'writtenBy', value))
                      }
                    />
                    <DecideFilter
                      decide={decide}
                      onChange={(value) => setParams(withSprintRetroValue(params, 'decide', value))}
                    />
                  </>
                )}
              </div>
              <div className="grid grid-cols-[minmax(0,1fr)_18rem] gap-4 touch:grid-cols-[minmax(0,1fr)]">
                <div className="min-w-0">
                  {view === 'category' ? (
                    <div data-testid={pagesTestIds.slotRetroByCategory}>
                      {touch ? (
                        <ByCategory
                          {...props}
                          writtenBy={writtenBy}
                          onAdded={clearHidingFilters}
                          category={cat}
                          onCategory={(value) =>
                            setParams(withSprintRetroValue(params, 'cat', value))
                          }
                          onRecurring={() => setSheet('recurring')}
                        />
                      ) : (
                        <ByCategory {...props} writtenBy={writtenBy} onAdded={clearHidingFilters} />
                      )}
                    </div>
                  ) : (
                    <ByWorkItem {...props} writtenBy={writtenBy} onAdded={clearHidingFilters} />
                  )}
                </div>
                {touch ? null : <RetroRail {...props} />}
              </div>
              {touch ? (
                <>
                  <RetroRailSheets retro={retro} open={sheet} onClose={() => setSheet(null)} />
                  <RetroFilterSheet
                    open={filterOpen}
                    writtenBy={writtenBy}
                    decide={decide}
                    onWrittenBy={(value) =>
                      setParams(withSprintRetroValue(params, 'writtenBy', value))
                    }
                    onDecide={(on) => setDecide(retro, on)}
                    onClear={() =>
                      setParams(
                        withSprintRetroValue(
                          withSprintRetroValue(params, 'writtenBy', 'all'),
                          'decide',
                          'all',
                        ),
                      )
                    }
                    onClose={() => setFilterOpen(false)}
                  />
                </>
              ) : null}
            </div>
          );
        }}
      </RetroQueryState>
    </RetroActionDialogProvider>
  );
}
