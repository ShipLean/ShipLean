import { useLocation } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { useRouteQuery } from '../../../pages/useRouteQuery';
import { LevelBacklogSlot } from '../../../slots/LevelBacklogSlot';

// CT-only: the slot with its level and mode taken from the address, as the page does, so a test can switch level or mode by
// navigation. The address is shown so a test can see where the mode switch went.
export function LevelSlotHarness({ project }: { project: ProjectDto }) {
  const { pathname, search } = useLocation();
  const level = pathname.endsWith('/epics') ? 'epics' : 'features';
  return (
    <>
      <LevelBacklogSlot project={project} level={level} mode={useRouteQuery().mode ?? 'list'} />
      <span data-testid="harness-address">{pathname + search}</span>
    </>
  );
}
