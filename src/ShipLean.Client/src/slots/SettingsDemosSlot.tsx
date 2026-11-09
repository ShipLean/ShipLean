import type { ProjectDto } from '@shiplean/core/api/projects';
import type { ProjectSettingsTab } from '../routes';
import { SettingsDemos } from '../features/settings/demos/SettingsDemos';

export interface SettingsDemosSlotProps {
  project: ProjectDto;
  tab: ProjectSettingsTab;
}

// The Demos tab's content; the settings draft frame shows it on that tab only. It has its own Save and takes no draft.
export function SettingsDemosSlot({ project }: SettingsDemosSlotProps) {
  return <SettingsDemos project={project} />;
}
