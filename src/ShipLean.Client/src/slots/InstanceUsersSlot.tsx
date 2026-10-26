import type { InstanceSettingsTab } from '../routes';
import { pagesTestIds } from '../features/pages/testIds';
import { InstanceUsers } from '../features/settings/instanceUsers/InstanceUsers';

export interface InstanceUsersSlotProps {
  tab: InstanceSettingsTab;
}

export function InstanceUsersSlot(props: InstanceUsersSlotProps) {
  void props;
  return (
    <div data-testid={pagesTestIds.slotInstanceUsers}>
      <InstanceUsers />
    </div>
  );
}
