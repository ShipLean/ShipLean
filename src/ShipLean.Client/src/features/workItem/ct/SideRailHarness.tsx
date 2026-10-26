import type { ProjectDto } from '../../../api/projects';
import { ToastHost } from '../../../components/ToastHost';
import { SideSlot } from '../../../slots/SideSlot';
import { project } from './WorkItemFormHarness';

// The side slot as the page mounts it, with the toasts. `role` is the caller's role in the project.
export function SideRailHarness({
  itemKey,
  role = 'admin',
}: {
  itemKey: string;
  role?: ProjectDto['role'];
}) {
  return (
    <>
      <SideSlot project={{ ...project, role }} itemKey={itemKey} />
      <ToastHost />
    </>
  );
}
