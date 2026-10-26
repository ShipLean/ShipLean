import type { InstanceSettingsTab } from '../routes';
import { pagesTestIds } from '../features/pages/testIds';
import { InstanceProjects } from '../features/settings/instance/InstanceProjects';

export interface InstanceProjectsSlotProps {
  tab: InstanceSettingsTab;
}

export function InstanceProjectsSlot(props: InstanceProjectsSlotProps) {
  void props;
  return (
    <div data-testid={pagesTestIds.slotInstanceProjects}>
      <InstanceProjects />
    </div>
  );
}
