import { useLocation } from 'react-router-dom';
import type { ProjectDto } from '../../../api/projects';
import { LevelBacklog } from '../levels/LevelBacklog';
import type { LevelBacklogLevel } from '../levels/levelColumns';

// CT-only: a level backlog with the address shown, so a test can see where the level picker went.
export function LevelHarness({
  project,
  level,
}: {
  project: ProjectDto;
  level: LevelBacklogLevel;
}) {
  const { pathname } = useLocation();
  return (
    <>
      <LevelBacklog project={project} level={level} />
      <span data-testid="harness-location">{pathname}</span>
    </>
  );
}
