import { useSearchParams } from 'react-router-dom';
import { SprintHeader } from '../features/sprintHeader/SprintHeader';
import { pagesTestIds } from '../features/pages/testIds';
import { TaskboardSettingsSlot } from '../slots/TaskboardSettingsSlot';
import { TaskboardSlot } from '../slots/TaskboardSlot';
import { pageRouteIds } from './pageRouteIds';
import { SprintScreen } from './SprintScreen';
import { taskboardSlots } from './taskboardSlots';

export function TaskboardPage() {
  const [params, setParams] = useSearchParams();
  return (
    <SprintScreen routeId={pageRouteIds.taskboard} testId={pagesTestIds.taskboardPage}>
      {(project, sprint, sprints) => {
        const slots = taskboardSlots(project, sprint, sprints, params, setParams);
        return (
          <>
            <SprintHeader {...slots.header} />
            <TaskboardSlot {...slots.taskboard} />
            <TaskboardSettingsSlot {...slots.taskboardSettings} />
          </>
        );
      }}
    </SprintScreen>
  );
}
