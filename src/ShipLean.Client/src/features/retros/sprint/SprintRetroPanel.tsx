import { useState } from 'react';
import type { ProjectDto } from '../../../api/projects';
import type { SprintDto } from '../../../api/sprints';
import { RetroQueryState } from '../RetroQueryState';
import { retrosTestIds as ids } from '../testIds';
import { useSprintRetro } from '../useRetroQueries';
import { AddPoint } from './AddPoint';
import { RetroRail } from './rail/RetroRail';
import { SprintRetroHeader } from './SprintRetroHeader';
import { ByCategory } from './views/ByCategory';
import { ByWorkItem } from './views/ByWorkItem';

type RetroView = 'category' | 'item';

const VIEWS = [
  { id: 'category', label: 'By category', testId: ids.retroViewByCategory },
  { id: 'item', label: 'By work item', testId: ids.retroViewByItem },
] as const;

/**
 * The sprint Retro tab: the header, the By category / By work item toggle (opens on By category), the chosen view and
 * the rail. The data is the sprint's SprintRetroDto; each part is its own component and takes it whole.
 */
export function SprintRetroPanel({ project, sprint }: { project: ProjectDto; sprint: SprintDto }) {
  const [view, setView] = useState<RetroView>('category');
  const query = useSprintRetro(project.id, sprint.id);
  return (
    <RetroQueryState query={query}>
      {(retro) => {
        const props = { project, sprint, retro };
        return (
          <div data-testid={ids.sprintRetroPanel} className="grid gap-4">
            <SprintRetroHeader {...props} />
            <div className="flex items-center gap-3">
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
                    onClick={() => setView(v.id)}
                  >
                    {v.label}
                  </button>
                ))}
              </div>
              <span className="flex-1" />
              <AddPoint {...props} />
            </div>
            <div className="grid grid-cols-[minmax(0,1fr)_20rem] gap-4">
              <div>
                {view === 'category' ? <ByCategory {...props} /> : <ByWorkItem {...props} />}
              </div>
              <RetroRail {...props} />
            </div>
          </div>
        );
      }}
    </RetroQueryState>
  );
}
