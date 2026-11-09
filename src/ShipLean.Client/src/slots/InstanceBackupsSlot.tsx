import type { InstanceSettingsTab } from '../routes';
import { pagesTestIds } from '../features/pages/testIds';
import { InstanceBackups } from '../features/settings/instance/backups/InstanceBackups';

export interface InstanceBackupsSlotProps {
  tab: InstanceSettingsTab;
}

export function InstanceBackupsSlot(props: InstanceBackupsSlotProps) {
  void props;
  return (
    <div data-testid={pagesTestIds.slotInstanceBackups}>
      <InstanceBackups />
    </div>
  );
}
