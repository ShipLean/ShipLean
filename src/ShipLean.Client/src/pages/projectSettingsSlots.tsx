import type { ProjectDto } from '../api/projects';
import type { ProjectSettingsTab } from '../routes';
import type { SettingsDraftSlotProps } from '../slots/SettingsDraftSlot';
import { SettingsIntegrationsSlot } from '../slots/SettingsIntegrationsSlot';
import { SettingsStatesSlot } from '../slots/SettingsStatesSlot';

// The settings draft slot is the frame of every tab (tab list with its dirty dots, the one Save and Discard); the
// States and Integrations content is handed to it and shown on those tabs.
export function projectSettingsSlots(
  project: ProjectDto,
  tab: ProjectSettingsTab,
): { settingsDraft: SettingsDraftSlotProps } {
  return {
    settingsDraft: {
      project,
      tab,
      states: <SettingsStatesSlot project={project} tab={tab} />,
      integrations: <SettingsIntegrationsSlot project={project} tab={tab} />,
    },
  };
}
