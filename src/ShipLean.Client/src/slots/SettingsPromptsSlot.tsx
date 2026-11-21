import type { ProjectDto } from '@shiplean/core/api/projects';
import { PromptsPage } from '../features/prompts/PromptsPage';
import type { ProjectSettingsTab } from '../routes';

export interface SettingsPromptsSlotProps {
  project: ProjectDto;
  tab: ProjectSettingsTab;
}

// The Prompts tab's content; the settings draft frame shows it on that tab only. Each prompt saves on its own and takes no draft.
export function SettingsPromptsSlot({ project }: SettingsPromptsSlotProps) {
  return <PromptsPage project={project} />;
}
