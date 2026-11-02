import { useSearchParams } from 'react-router-dom';
import type { ProjectDto } from '../../../api/projects';
import type { SprintDto } from '../../../api/sprints';
import { pagesTestIds } from '../../pages/testIds';
import { RetroQueryState } from '../RetroQueryState';
import { retrosTestIds as ids } from '../testIds';
import { useSprintRetro } from '../useRetroQueries';
import { AddPoint } from './AddPoint';
import { RetroRail } from './rail/RetroRail';
import { SprintRetroHeader } from './SprintRetroHeader';
import { parseSprintRetroAddress, withSprintRetroValue } from './sprintRetroAddress';
import { ByCategory } from './views/ByCategory';
import { ByWorkItem } from './views/ByWorkItem';
import { WrittenByFilter } from './views/byCategory/WrittenByFilter';

const VIEWS = [
  { id: 'category', label: 'By category', testId: ids.retroViewByCategory },
  { id: 'item', label: 'By work item', testId: ids.retroViewByItem },
] as const;

/**
 * The sprint Retro tab: the header, the By category / By work item toggle (opens on By category), the chosen view and
 * the rail. The view, Written by, decide and point live in the address (sprintRetroAddress.ts), not in state. The data is the sprint's SprintRetroDto; each part is its own component and takes it whole.
 */
export function SprintRetroPanel({ project, sprint }: { project: ProjectDto; sprint: SprintDto }) {
  const [params, setParams] = useSearchParams();
  const { view, writtenBy, decide, point } = parseSprintRetroAddress(params);
  const query = useSprintRetro(project.id, sprint.id);
  return (
    <RetroQueryState query={query}>
      {(retro) => {
        const props = { project, sprint, retro, decide, point };
        return (
          <div
            data-testid={ids.sprintRetroPanel}
            className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-4"
          >
            <SprintRetroHeader {...props} />
            <div className="flex flex-wrap items-center gap-3">
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
                    className="cursor-pointer border-r border-edge bg-panel px-3 py-1 text-[length:var(--text-sm)] text-ink last:border-r-0 aria-pressed:bg-canvas aria-pressed:font-semibold"
                    onClick={() => setParams(withSprintRetroValue(params, 'view', v.id))}
                  >
                    {v.label}
                  </button>
                ))}
              </div>
              <span className="flex-1" />
              <WrittenByFilter
                value={writtenBy}
                onChange={(value) => setParams(withSprintRetroValue(params, 'writtenBy', value))}
              />
              <AddPoint {...props} />
            </div>
            <div className="grid grid-cols-[minmax(0,1fr)_18rem] gap-4">
              <div className="min-w-0">
                {view === 'category' ? (
                  <div data-testid={pagesTestIds.slotRetroByCategory}>
                    <ByCategory {...props} writtenBy={writtenBy} />
                  </div>
                ) : (
                  <ByWorkItem {...props} writtenBy={writtenBy} />
                )}
              </div>
              <RetroRail {...props} />
            </div>
          </div>
        );
      }}
    </RetroQueryState>
  );
}
