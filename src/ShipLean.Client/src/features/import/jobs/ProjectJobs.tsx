import type { ProjectDto } from '../../../api/projects';
import { JobsPanel } from './JobsPanel';

// "Import jobs" under the stepper. Only an admin can read the list, so a member's page asks for nothing.
export function ProjectJobs({ project }: { project: ProjectDto }) {
  return project.role === 'admin' ? <JobsPanel project={project} /> : null;
}
