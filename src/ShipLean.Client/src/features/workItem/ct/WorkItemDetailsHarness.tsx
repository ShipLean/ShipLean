import { Link, useLocation } from 'react-router-dom';
import type { ProjectDto, ProjectRole } from '../../../api/projects';
import { ToastHost } from '../../../components/ToastHost';
import { DiscussionSlot } from '../../../slots/DiscussionSlot';
import { FormSlot } from '../../../slots/FormSlot';
import { TasksSlot } from '../../../slots/TasksSlot';
import { workItemHarnessIds as harness } from './harnessIds';
import { project } from './WorkItemFormHarness';

// The form slot with the real tasks and discussion slots in their nodes, so a test can see whether a task or a comment
// touches the form. `role` is the caller's role in the project.
export function WorkItemDetailsHarness({
  itemKey,
  role = 'admin',
}: {
  itemKey: string;
  role?: ProjectRole;
}) {
  const location = useLocation();
  const as: ProjectDto = { ...project, role };
  return (
    <>
      <FormSlot
        project={as}
        itemKey={itemKey}
        links={<div data-testid={harness.links} />}
        tasks={<TasksSlot project={as} itemKey={itemKey} />}
        discussion={<DiscussionSlot project={as} itemKey={itemKey} />}
        side={<div data-testid={harness.side} />}
      />
      <Link data-testid={harness.leave} to="/elsewhere">
        leave
      </Link>
      <output data-testid={harness.location}>{location.pathname}</output>
      <ToastHost />
    </>
  );
}
