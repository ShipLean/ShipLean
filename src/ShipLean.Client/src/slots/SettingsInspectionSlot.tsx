import type { ProjectDto } from '@shiplean/core/api/projects';
import { InspectionSettings } from '../features/inspection/InspectionSettings';
import type { ProjectSettingsTab } from '../routes';

export interface SettingsInspectionSlotProps {
  project: ProjectDto;
  tab: ProjectSettingsTab;
}

// The Inspection tab's content; the settings draft frame shows it on that tab only. It has its own Save and takes no draft.
export function SettingsInspectionSlot({ project }: SettingsInspectionSlotProps) {
  return <InspectionSettings project={project} />;
}
