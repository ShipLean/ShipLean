import type { FormSlotProps } from '../../../slots/FormSlot';
import { workItemTestIds as ids } from '../testIds';
import { useWorkItem } from '../workItemApi';
import { WorkItemForm } from './WorkItemForm';

// Loads the item the page's route names, then shows its form.
export function WorkItemScreen({
  project,
  itemKey,
  links,
  tasks,
  discussion,
  side,
}: FormSlotProps) {
  const item = useWorkItem(project.id, itemKey);
  if (item.isPending) {
    return (
      <div data-testid={ids.loading} className="p-4 text-ink-muted">
        Loading…
      </div>
    );
  }
  // A failed refetch of an item already on screen keeps the form (and its draft); the form says the reload failed.
  if (item.data === undefined) {
    return (
      <div data-testid={ids.loadFailed} role="alert" className="p-4 text-alert">
        {item.error?.message}
      </div>
    );
  }
  return (
    <WorkItemForm
      key={item.data.id}
      item={item.data}
      project={project}
      links={links}
      tasks={tasks}
      discussion={discussion}
      side={side}
    />
  );
}
