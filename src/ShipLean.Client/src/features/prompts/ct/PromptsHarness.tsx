import { useSearchParams } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { PromptsPage } from '../PromptsPage';

/** The page with the address's query printed, so a spec can see what selecting a prompt did to the router. */
export function PromptsHarness({ project }: { project: ProjectDto }) {
  const [searchParams] = useSearchParams();
  return (
    <>
      <output data-testid="harness-search">{searchParams.toString()}</output>
      <PromptsPage project={project} />
    </>
  );
}
