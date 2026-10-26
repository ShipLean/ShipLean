import { useEffect, useRef, useState } from 'react';
import type { WorkItemDto } from '../../../api/workItems';
import { TypeIcon } from '../../../components/TypeIcon';
import { inputClass } from '../form/inputClasses';
import { workItemTestIds as ids } from '../testIds';
import { useAddTask } from './useAddTask';

// The inline "+ Task" row. Enter or Add creates the task at once (it is its own work item, not part of the form's Save);
// Esc or Cancel closes it.
export function AddTaskForm({
  projectId,
  itemKey,
  parent,
  onClose,
}: {
  projectId: string;
  itemKey: string;
  parent: WorkItemDto;
  onClose: () => void;
}) {
  const [title, setTitle] = useState('');
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => input.current?.focus(), []);
  const add = useAddTask(projectId, itemKey, parent);
  const trimmed = title.trim();
  const submit = () => {
    if (trimmed === '' || add.isPending) return;
    add.mutate(trimmed, {
      onSuccess: () => {
        setTitle('');
      },
    });
  };
  const error = add.error?.message;
  return (
    <form
      data-testid={ids.taskForm}
      className="grid gap-1 border-t border-edge px-2 py-1.5"
      onSubmit={(event) => {
        event.preventDefault();
        submit();
      }}
    >
      <div className="flex items-center gap-2">
        <TypeIcon type="task" />
        <input
          data-testid={ids.taskInput}
          aria-label="Task title"
          placeholder="Task title, then Enter"
          className={`${inputClass} min-w-0 flex-1`}
          value={title}
          ref={input}
          onChange={(event) => setTitle(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Escape') {
              event.preventDefault();
              event.stopPropagation();
              onClose();
            }
          }}
        />
        <button
          type="submit"
          data-testid={ids.taskSubmit}
          disabled={trimmed === '' || add.isPending}
          className="h-7 rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-semibold text-signal-ink disabled:opacity-50"
        >
          Add
        </button>
        <button
          type="button"
          data-testid={ids.taskCancel}
          className="h-7 rounded-control px-2 text-[length:var(--text-sm)] text-ink-muted hover:bg-signal-soft"
          onClick={onClose}
        >
          Cancel
        </button>
      </div>
      {error ? (
        <span
          data-testid={ids.taskError}
          role="alert"
          className="text-[length:var(--text-xs)] text-alert"
        >
          {error}
        </span>
      ) : null}
    </form>
  );
}
