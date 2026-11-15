import type { ProjectDto } from '@shiplean/core/api/projects';
import { inspectionTestIds as ids } from './testIds';

export interface InspectionSettingsGeneralProps {
  project: ProjectDto;
}

// The General sub-tab: Inspection on or off for the project, content display, tenant and selector overrides.
// TODO: E1 (SHIP-1570) builds the form and its Save here (load, form and useSaveInspectionSettings), outside the settings draft.
export function InspectionSettingsGeneral({ project }: InspectionSettingsGeneralProps) {
  return <div data-testid={ids.inspectionGeneral} data-project={project.key} />;
}
