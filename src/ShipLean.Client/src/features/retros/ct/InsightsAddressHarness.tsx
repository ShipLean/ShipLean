import { useLocation } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { RetroInsightsPanel } from '../insights/RetroInsightsPanel';

/** The Insights panel with the address it writes shown beside it, since a component test has no browser bar to read. */
export function InsightsAddressHarness({ project }: { project: ProjectDto }) {
  const { search } = useLocation();
  return (
    <>
      <span data-testid="test-address">{search}</span>
      <RetroInsightsPanel project={project} />
    </>
  );
}
