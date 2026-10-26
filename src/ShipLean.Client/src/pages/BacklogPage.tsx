import { pagesTestIds } from '../features/pages/testIds';
import type { Level } from '../routes';
import { BacklogSlots } from './BacklogSlots';
import { backlogRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';

// Stories open in the mode ?mode= names (list when absent); Features and Epics have the one list. The sprint end
// prompt is mounted on every backlog page.
export function BacklogPage({ level }: { level: Level }) {
  return (
    <ProjectScreen routeId={backlogRouteIds[level]} testId={pagesTestIds.backlogPage}>
      {(project) => <BacklogSlots project={project} level={level} />}
    </ProjectScreen>
  );
}
