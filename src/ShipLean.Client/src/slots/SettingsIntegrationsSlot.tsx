import type { ProjectDto } from '../api/projects';
import type { ProjectSettingsTab } from '../routes';
import { pagesTestIds } from '../features/pages/testIds';
import { IssueIntakeSection } from '../features/settings/issueIntake/IssueIntakeSection';
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
      <div className="px-4">
        <IssueIntakeSection project={project} />
      </div>
    </div>
  );
}
