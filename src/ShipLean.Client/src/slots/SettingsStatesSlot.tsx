import type { ProjectDto } from '../api/projects';
import type { ProjectSettingsTab } from '../routes';
import { pagesTestIds } from '../features/pages/testIds';
import { SettingsStates } from '../features/settings/states/SettingsStates';

export interface SettingsStatesSlotProps {
  project: ProjectDto;
  tab: ProjectSettingsTab;
}

export function SettingsStatesSlot({ project }: SettingsStatesSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotSettingsStates}>
      <SettingsStates projectId={project.id} />
    </div>
  );
}
