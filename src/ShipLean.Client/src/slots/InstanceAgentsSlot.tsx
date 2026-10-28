import type { InstanceSettingsTab } from '../routes';
import { pagesTestIds } from '../features/pages/testIds';
import { InstanceAgents } from '../features/settings/instance/agents/InstanceAgents';

export interface InstanceAgentsSlotProps {
  tab: InstanceSettingsTab;
}

export function InstanceAgentsSlot(props: InstanceAgentsSlotProps) {
  void props;
  return (
    <div data-testid={pagesTestIds.slotInstanceAgents}>
      <InstanceAgents />
    </div>
  );
}
