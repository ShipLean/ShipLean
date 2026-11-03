import { useLocation } from 'react-router-dom';
import { WorkItemRetroSection } from '../WorkItemRetroSection';

// The section beside the router's current path and query, so a spec can see where a row went (CT only).
export function WorkItemRetroLocationHarness(props: {
  projectId: string;
  projectKey: string;
  itemKey: string;
  workItemId: string;
}) {
  const location = useLocation();
  return (
    <>
      <WorkItemRetroSection {...props} />
      <span data-testid="location-probe">
        {location.pathname}
        {location.search}
      </span>
    </>
  );
}
