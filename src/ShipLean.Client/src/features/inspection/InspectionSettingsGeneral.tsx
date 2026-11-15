import type { ProjectDto } from '@shiplean/core/api/projects';
import { errorText } from '../settings/instance/model';
import { GeneralForm } from './settings/general/GeneralForm';
import { useInspectionSettings } from './inspectionApi';
import { inspectionTestIds as ids } from './testIds';

export interface InspectionSettingsGeneralProps {
  project: ProjectDto;
}

// The General sub-tab: Inspection on or off for the project, content display, tenant and selector overrides. It loads
// the settings and owns Save, outside the settings draft (D30).
export function InspectionSettingsGeneral({ project }: InspectionSettingsGeneralProps) {
  const settings = useInspectionSettings(project.key);
  if (settings.data) return <GeneralForm project={project} settings={settings.data} />;
  if (settings.isError) {
    return (
      <p role="alert" data-testid={ids.inspectionSettingsLoadFailed} className="p-4 text-alert">
        {errorText(settings.error)}
      </p>
    );
  }
  return (
    <p data-testid={ids.inspectionSettingsLoading} className="p-4 text-ink-muted">
      Loading…
    </p>
  );
}
