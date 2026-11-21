import type { ProjectDto } from '@shiplean/core/api/projects';
import { PromptsPage } from '../PromptsPage';
import { PromptsTabBadge } from '../PromptsTabBadge';

/** The page with the tab badge beside it, as the settings tab list shows it. */
export function PromptsBadgeHarness({ project }: { project: ProjectDto }) {
  return (
    <>
      <PromptsTabBadge projectId={project.id} />
      <PromptsPage project={project} />
    </>
  );
}
