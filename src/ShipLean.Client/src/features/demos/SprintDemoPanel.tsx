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

// TODO: the laid-out Demo tab: loading and failed states, the band over the player and its side lists.
export function SprintDemoPanel({ project, sprint }: { project: ProjectDto; sprint: SprintDto }) {
  const query = useSprintDemo(project.id, sprint.id);
  useSprintDemoRealtime(project.id, sprint.id);
  const player = useMemo(() => newPlayerHandle(), []);
  const demo = query.data;
  return (
    <div data-testid={ids.demoPanel}>
      {demo === undefined ? (
        <p>{query.isError ? query.error.message : 'Loading the demo'}</p>
      ) : (
        <>
          <DemoStatusBand demo={demo} player={player} />
          <DemoPlayer demo={demo} player={player} />
          <DemoChapters demo={demo} player={player} />
          <DemoFeatures demo={demo} player={player} />
          <DemoScript demo={demo} player={player} />
          <DemoYouTubeNotice demo={demo} player={player} />
        </>
      )}
    </div>
  );
}
