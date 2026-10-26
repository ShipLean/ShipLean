import type { ProjectDto } from '../api/projects';
import type { ImportRunSlotProps } from '../slots/ImportRunSlot';
import type { ImportStepperSlotProps } from '../slots/ImportStepperSlot';

// Jira import into a project (/p/:key/import) or with none yet (/import: null).
export function importSlots(project: ProjectDto | null): {
  importStepper: ImportStepperSlotProps;
  importRun: ImportRunSlotProps;
} {
  return { importStepper: { project }, importRun: { project } };
}
