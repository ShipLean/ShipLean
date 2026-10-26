import { useQueryClient } from '@tanstack/react-query';
import { Link, useLocation } from 'react-router-dom';
import type { ProjectDto } from '../../../api/projects';
import { ToastHost } from '../../../components/ToastHost';
import { FormSlot } from '../../../slots/FormSlot';
import { LinksSlot } from '../../../slots/LinksSlot';
import { UnsavedGuardHost } from '../form/UnsavedGuardHost';
import { workItemKey } from '../workItemApi';
import { workItemHarnessIds as harness } from './harnessIds';

export const project: ProjectDto = {
  id: 'p1',
  key: 'SHIP',
  name: 'Shipping',
  description: '',
  role: 'admin',
  memberCount: 2,
  workingDays: [1, 2, 3, 4, 5],
  velocityWindow: 3,
  velocityOverride: null,
  nextItemNumber: 200,
};

// The form slot as the page mounts it, with the other four nodes as markers, a link out of the page, and the toasts.
// `realLinks` mounts the links slot itself in the links node, in place of a marker.
export function WorkItemFormHarness({
  itemKey,
  realLinks = false,
}: {
  itemKey: string;
  realLinks?: boolean;
}) {
  const location = useLocation();
  const queryClient = useQueryClient();
  return (
    <>
      <UnsavedGuardHost />
      <FormSlot
        project={project}
        itemKey={itemKey}
        links={
          realLinks ? (
            <LinksSlot project={project} itemKey={itemKey} />
          ) : (
            <div data-testid={harness.links} />
          )
        }
        tasks={<div data-testid={harness.tasks} />}
        discussion={<div data-testid={harness.discussion} />}
        side={<div data-testid={harness.side} />}
      />
      <Link data-testid={harness.leave} to="/elsewhere">
        leave
      </Link>
      <button
        type="button"
        data-testid={harness.refetch}
        onClick={() =>
          void queryClient.invalidateQueries({ queryKey: workItemKey(project.id, itemKey) })
        }
      >
        refetch
      </button>
      <output data-testid={harness.location}>{location.pathname}</output>
      <ToastHost />
    </>
  );
}
