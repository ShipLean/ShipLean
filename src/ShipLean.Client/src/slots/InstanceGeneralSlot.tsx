import type { InstanceSettingsTab } from '../routes';
import { pagesTestIds } from '../features/pages/testIds';
import { InstanceGeneral } from '../features/settings/instance/InstanceGeneral';

export interface InstanceGeneralSlotProps {
  tab: InstanceSettingsTab;
}

export function InstanceGeneralSlot(props: InstanceGeneralSlotProps) {
  void props;
  return (
    <div data-testid={pagesTestIds.slotInstanceGeneral}>
      <InstanceGeneral />
    </div>
  );
}
