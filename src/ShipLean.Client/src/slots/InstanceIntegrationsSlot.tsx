import type { InstanceSettingsTab } from '../routes';
import { pagesTestIds } from '../features/pages/testIds';
import { InstanceIntegrations } from '../features/settings/instance/integrations/InstanceIntegrations';

export interface InstanceIntegrationsSlotProps {
  tab: InstanceSettingsTab;
}

export function InstanceIntegrationsSlot(props: InstanceIntegrationsSlotProps) {
  void props;
  return (
    <div data-testid={pagesTestIds.slotInstanceIntegrations}>
      <InstanceIntegrations />
    </div>
  );
}
