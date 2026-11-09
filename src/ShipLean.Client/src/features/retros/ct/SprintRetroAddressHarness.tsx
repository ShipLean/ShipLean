import { useLocation } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { SprintRetroPanel } from '../sprint/SprintRetroPanel';

/** The sprint Retro tab with the address it writes shown beside it, since a component test has no browser bar to read. */
export function SprintRetroAddressHarness({
  project,
  sprint,
}: {
  project: ProjectDto;
  sprint: SprintDto;
}) {
  const { search } = useLocation();
  return (
    <>
      <span data-testid="test-address">{search}</span>
      <SprintRetroPanel project={project} sprint={sprint} />
    </>
  );
}
