import type { ProjectDto } from '../api/projects';
import type { ProjectSettingsTab } from '../routes';
import { pagesTestIds } from '../features/pages/testIds';
import { IntegrationsTab } from '../features/import/integrations/IntegrationsTab';

export interface SettingsIntegrationsSlotProps {
  project: ProjectDto;
  tab: ProjectSettingsTab;
}

// The Integrations tab's content; the settings draft frame shows it on that tab only.
export function SettingsIntegrationsSlot({ project }: SettingsIntegrationsSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotSettingsIntegrations}>
      <IntegrationsTab project={project} />
    </div>
  );
}
