import { useParams } from 'react-router-dom';
import { ChangesScreen } from '../features/taskChanges/ChangesScreen';
import { taskChangesTestIds } from '../features/taskChanges/testIds';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';

// A Task or Bug's commits, diff and code comments (design D1). Loaded lazily: the diff renderer comes with it.
export function TaskChangesPage() {
  const { itemKey = '' } = useParams();
  return (
    <ProjectScreen routeId={pageRouteIds.taskChanges} testId={taskChangesTestIds.page}>
      {(project) => <ChangesScreen project={project} itemKey={itemKey} />}
    </ProjectScreen>
  );
}
