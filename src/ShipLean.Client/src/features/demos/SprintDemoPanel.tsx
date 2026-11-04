import { useMemo } from 'react';
import type { ProjectDto } from '../../api/projects';
import type { SprintDto } from '../../api/sprints';
import { DemoChapters } from './DemoChapters';
import { DemoFeatures } from './DemoFeatures';
import { DemoPlayer } from './DemoPlayer';
import { DemoScript } from './DemoScript';
import { DemoStatusBand } from './DemoStatusBand';
import { DemoYouTubeNotice } from './DemoYouTubeNotice';
import { useSprintDemo, useSprintDemoRealtime } from './demosApi';
import { newPlayerHandle } from './playerHandle';
import { demosTestIds as ids } from './testIds';

/** The Demo tab: the status band over the player and its side lists, then the script. */
export function SprintDemoPanel({ project, sprint }: { project: ProjectDto; sprint: SprintDto }) {
  const query = useSprintDemo(project.id, sprint.id);
  useSprintDemoRealtime(project.id, sprint.id);
  const player = useMemo(() => newPlayerHandle(), []);
  const demo = query.data;
  return (
    <div data-testid={ids.demoPanel} className="grid max-w-[1280px] content-start gap-3">
      {demo === undefined ? (
        query.isError ? (
          <div
            role="alert"
            className="flex flex-wrap items-center gap-3 rounded-panel border border-l-[3px] border-edge border-l-alert bg-panel px-3 py-2 text-[length:var(--text-sm)]"
          >
            <span className="min-w-60 flex-1 [overflow-wrap:anywhere]">
              Could not load the demo: {query.error.message}
            </span>
            <button
              type="button"
              data-testid={ids.demoLoadRetry}
              onClick={() => void query.refetch()}
              className="h-7 rounded-control border border-edge bg-panel px-3 font-medium hover:bg-sunken"
            >
              Retry
            </button>
          </div>
        ) : (
          <p className="text-ink-muted">Loading the demo</p>
        )
      ) : (
        <>
          <DemoStatusBand demo={demo} player={player} />
          {demo.video !== null && (
            <>
              <div className="grid grid-cols-[minmax(0,1fr)_360px] items-start gap-4">
                <div className="grid min-w-0 gap-2">
                  <DemoPlayer demo={demo} player={player} />
                  <DemoYouTubeNotice demo={demo} player={player} />
                </div>
                <div className="grid min-w-0 gap-3">
                  <DemoChapters demo={demo} player={player} />
                  <DemoFeatures demo={demo} player={player} />
                </div>
              </div>
              <DemoScript demo={demo} player={player} />
            </>
          )}
        </>
      )}
    </div>
  );
}
