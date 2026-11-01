import { pagesTestIds } from '../features/pages/testIds';
import { RetrosSearchSlot } from '../slots/RetrosSearchSlot';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';
import { retrosSlots } from './retrosSlots';

export function RetrosPage() {
  return (
    <ProjectScreen routeId={pageRouteIds.retros} testId={pagesTestIds.retrosPage}>
      {(project) => <RetrosSearchSlot {...retrosSlots(project).retrosSearch} />}
    </ProjectScreen>
  );
}
