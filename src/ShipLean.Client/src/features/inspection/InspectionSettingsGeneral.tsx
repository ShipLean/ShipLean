import type { ProjectDto } from '@shiplean/core/api/projects';
import { inspectionTestIds as ids } from './testIds';

export interface InspectionSettingsGeneralProps {
  project: ProjectDto;
}

// The General sub-tab: Inspection on or off for the project, content display, tenant and selector overrides.
// TODO: the form replaces this stub. It saves through the frame's Save, not a draft.
export function InspectionSettingsGeneral({ project }: InspectionSettingsGeneralProps) {
  return <div data-testid={ids.inspectionEnabled} data-project={project.key} />;
}
