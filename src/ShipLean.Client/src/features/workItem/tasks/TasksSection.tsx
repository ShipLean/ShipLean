import { useState } from 'react';
import type { ProjectDto } from '../../../api/projects';
import { Chip } from '../../../components/Chip';
import { workItemTestIds as ids } from '../testIds';
import { useWorkItem } from '../workItemApi';
import { AddTaskForm } from './AddTaskForm';
import { TaskRow } from './TaskRow';
import { useChildEvents } from './useChildEvents';

// The item's child tasks. Each is its own work item and saves straight away, so nothing here touches the form's draft.
export function TasksSection({ project, itemKey }: { project: ProjectDto; itemKey: string }) {
  const item = useWorkItem(project.id, itemKey).data;
  useChildEvents(project.id, itemKey, item);
  const [adding, setAdding] = useState(false);
  if (!item) return null;
  const tasks = item.children.filter((child) => child.type.id === 'task');
  const done = tasks.filter((task) => task.state.category === 'completed').length;
  return (
    <section data-testid={ids.tasks} aria-label="Tasks" className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-2">
      <header className="flex items-center gap-2">
        <h2 className="text-[length:var(--text-md)] font-semibold">Tasks</h2>
        <span data-testid={ids.tasksCount}>
          <Chip tone={tasks.length > 0 && done === tasks.length ? 'ok' : 'muted'}>
            {done}/{tasks.length}
          </Chip>
        </span>
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          Each task is its own work item and saves straight away
        </span>
        <button
          type="button"
          data-testid={ids.taskAdd}
          className="ml-auto h-6 rounded-control px-2 text-[length:var(--text-xs)] text-signal hover:bg-signal-soft"
          onClick={() => setAdding(!adding)}
        >
          + Task
        </button>
      </header>
      <div className="rounded-control border border-edge bg-panel">
        {tasks.length === 0 && !adding ? (
          <p
            data-testid={ids.tasksEmpty}
            className="m-0 p-3 text-center text-[length:var(--text-sm)] text-ink-muted"
          >
            No tasks yet. Break it down when someone picks it up.
          </p>
        ) : (
          <ul className="m-0 grid list-none grid-cols-[minmax(0,1fr)] p-0">
            {tasks.map((task) => (
              <TaskRow key={task.id} task={task} project={project} parentKey={itemKey} />
            ))}
          </ul>
        )}
        {adding ? (
          <AddTaskForm
            projectId={project.id}
            itemKey={itemKey}
            parent={item}
            onClose={() => setAdding(false)}
          />
        ) : null}
      </div>
    </section>
  );
}
