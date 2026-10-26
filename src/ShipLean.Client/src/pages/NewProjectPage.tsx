import { pagesTestIds } from '../features/pages/testIds';
import { NewProjectSlot } from '../slots/NewProjectSlot';
import { pageRouteIds } from './pageRouteIds';
import { InstallScreen } from './InstallScreen';

export function NewProjectPage() {
  return (
    <InstallScreen routeId={pageRouteIds.newProject} testId={pagesTestIds.newProjectPage}>
      <NewProjectSlot />
    </InstallScreen>
  );
}
