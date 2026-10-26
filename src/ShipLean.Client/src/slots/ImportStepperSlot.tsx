import type { ProjectDto } from '../api/projects';
import { CreateProjectFirst } from '../features/import/stepper/CreateProjectFirst';
import { ImportStepper } from '../features/import/stepper/ImportStepper';
import { pagesTestIds } from '../features/pages/testIds';

export interface ImportStepperSlotProps {
  project: ProjectDto | null;
}

// With no project (/import) the project is created first; with one, the stepper imports into it.
export function ImportStepperSlot({ project }: ImportStepperSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotImportStepper}>
      {project === null ? <CreateProjectFirst /> : <ImportStepper project={project} />}
    </div>
  );
}
