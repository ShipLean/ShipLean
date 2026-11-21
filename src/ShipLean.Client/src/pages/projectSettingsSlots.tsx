import type { ProjectDto } from '@shiplean/core/api/projects';
import type { ProjectSettingsTab } from '../routes';
import type { SettingsDraftSlotProps } from '../slots/SettingsDraftSlot';
import { SettingsDemosSlot } from '../slots/SettingsDemosSlot';
import { SettingsInspectionSlot } from '../slots/SettingsInspectionSlot';
import { SettingsIntegrationsSlot } from '../slots/SettingsIntegrationsSlot';
import { SettingsPromptsSlot } from '../slots/SettingsPromptsSlot';
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
      demos: <SettingsDemosSlot project={project} tab={tab} />,
      inspection: <SettingsInspectionSlot project={project} tab={tab} />,
      prompts: <SettingsPromptsSlot project={project} tab={tab} />,
    },
  };
}
