import type { InstanceSettingsTab } from '../routes';
import type { InstanceGeneralSlotProps } from '../slots/InstanceGeneralSlot';
import type { InstanceAgentsSlotProps } from '../slots/InstanceAgentsSlot';
import type { InstanceIntegrationsSlotProps } from '../slots/InstanceIntegrationsSlot';
import type { InstanceAnalyticsSlotProps } from '../slots/InstanceAnalyticsSlot';
import type { InstanceProjectsSlotProps } from '../slots/InstanceProjectsSlot';
import type { InstanceUsersSlotProps } from '../slots/InstanceUsersSlot';

export interface InstanceSettingsSlots {
  instanceGeneral?: InstanceGeneralSlotProps;
  instanceUsers?: InstanceUsersSlotProps;
  instanceProjects?: InstanceProjectsSlotProps;
  instanceIntegrations?: InstanceIntegrationsSlotProps;
  instanceAnalytics?: InstanceAnalyticsSlotProps;
  instanceAgents?: InstanceAgentsSlotProps;
}

// Each instance settings tab has its own slot.
export function instanceSettingsSlots(tab: InstanceSettingsTab): InstanceSettingsSlots {
  return {
    instanceGeneral: tab === 'general' ? { tab } : undefined,
    instanceUsers: tab === 'users' ? { tab } : undefined,
    instanceProjects: tab === 'projects' ? { tab } : undefined,
    instanceIntegrations: tab === 'integrations' ? { tab } : undefined,
    instanceAnalytics: tab === 'analytics' ? { tab } : undefined,
    instanceAgents: tab === 'agents' ? { tab } : undefined,
  };
}
