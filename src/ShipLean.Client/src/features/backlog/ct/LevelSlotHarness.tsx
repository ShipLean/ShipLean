import { useLocation } from 'react-router-dom';
import type { ProjectDto } from '../../../api/projects';
import { LevelBacklogSlot } from '../../../slots/LevelBacklogSlot';

// CT-only: the slot with its level taken from the address, as the page does, so a test can switch level by navigation.
export function LevelSlotHarness({ project }: { project: ProjectDto }) {
  const { pathname } = useLocation();
  const level = pathname.endsWith('/epics') ? 'epics' : 'features';
  return <LevelBacklogSlot project={project} level={level} mode="list" />;
}
