import { useParams } from 'react-router-dom';
import { pagesTestIds } from '../features/pages/testIds';
import { ImportSlots } from './ImportSlots';
import { pageRouteIds } from './pageRouteIds';
import { InstallScreen } from './InstallScreen';
import { ProjectScreen } from './ProjectScreen';

// Jira import, at /p/:key/import (into that project) and at /import (no project yet, the stepper picks or creates one).
export function ImportPage() {
  const { key } = useParams();
  if (key === undefined) {
    return (
      <InstallScreen routeId={pageRouteIds.importWithoutProject} testId={pagesTestIds.importPage}>
        <ImportSlots project={null} />
      </InstallScreen>
    );
  }
  return (
    <ProjectScreen routeId={pageRouteIds.importIntoProject} testId={pagesTestIds.importPage}>
      {(project) => <ImportSlots project={project} />}
    </ProjectScreen>
  );
}
