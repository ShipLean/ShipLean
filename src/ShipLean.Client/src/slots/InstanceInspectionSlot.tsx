import type { InstanceSettingsTab } from '../routes';
import { pagesTestIds } from '../features/pages/testIds';
import { InspectionHealthPanel } from '../features/inspection/health/InspectionHealthPanel';

export interface InstanceInspectionSlotProps {
  tab: InstanceSettingsTab;
}

export function InstanceInspectionSlot(props: InstanceInspectionSlotProps) {
  void props;
  return (
    <div data-testid={pagesTestIds.slotInstanceInspection}>
      <InspectionHealthPanel />
    </div>
  );
}
