import type { ProjectDto } from '../api/projects';
import { ProjectJobs } from '../features/import/jobs/ProjectJobs';
import { pagesTestIds } from '../features/pages/testIds';

export interface ImportRunSlotProps {
  project: ProjectDto | null;
}

// Under the stepper: the project's earlier import jobs, with Re-run.
export function ImportRunSlot({ project }: ImportRunSlotProps) {
  return (
    <div data-testid={pagesTestIds.slotImportRun}>
      {project === null ? null : <ProjectJobs project={project} />}
    </div>
  );
}
