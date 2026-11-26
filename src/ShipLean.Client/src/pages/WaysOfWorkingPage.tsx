import { Navigate, useParams, useSearchParams } from 'react-router-dom';
import { pagesTestIds } from '../features/pages/testIds';
import { PromptsPage } from '../features/prompts/PromptsPage';
import { routePath } from '../routes';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';

// Ways of working (SHIP-2280): the project's agent prompts, a page of its own in the side menu. The prompt open is in
// ?prompt=, which PromptsPage reads.
export function WaysOfWorkingPage() {
  return (
    <ProjectScreen routeId={pageRouteIds.waysOfWorking} testId={pagesTestIds.waysOfWorkingPage}>
      {(project) => <PromptsPage project={project} />}
    </ProjectScreen>
  );
}

// The old Prompts tab's address (/p/:key/settings/prompts) goes to Ways of working, keeping the prompt open and the open
// item. It replaces, so Back does not land on the redirect again.
export function OldPromptsSettingsRedirect() {
  const { key = '' } = useParams();
  const [params] = useSearchParams();
  const prompt = params.get('prompt') ?? undefined;
  const item = params.get('item') ?? undefined;
  return <Navigate replace to={routePath('waysOfWorking', { key }, { prompt, item })} />;
}
