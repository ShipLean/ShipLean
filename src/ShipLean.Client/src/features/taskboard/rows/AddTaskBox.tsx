import { useRef, useState, type KeyboardEvent } from 'react';
import { ApiRequestError } from '../../../api/client';
import { useCreateOnTaskboard } from '../taskboardApi';
import { taskboardTestIds as ids } from '../testIds';

/** "+ Task" in a row: a title box that creates a child task and stays open for the next one. Esc closes it. */
export function AddTaskBox({
  projectId,
  sprintId,
  parentId,
}: {
  projectId: string;
  sprintId: string;
  parentId: string | null;
}) {
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState('');
  const input = useRef<HTMLInputElement>(null);
  const create = useCreateOnTaskboard(projectId, sprintId);

  function add() {
    const text = title.trim();
    if (text === '' || create.isPending) return;
    create.mutate(
      { type: 'task', title: text, parentId },
      {
        onSuccess: () => {
          setTitle('');
          input.current?.focus();
        },
      },
    );
  }

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === 'Enter') add();
    else if (event.key === 'Escape') setOpen(false);
  }

  if (!open) {
    return (
      <button
        type="button"
        data-testid={ids.addTask}
        className="btn-ghost justify-self-start"
        onClick={() => setOpen(true)}
      >
        + Task
      </button>
    );
  }
  return (
    <div data-testid={ids.taskBox} className="grid gap-1">
      <input
        ref={input}
        autoFocus
        data-testid={ids.taskTitle}
        aria-label="New task title"
        className="field"
        placeholder="Task title, then Enter"
        value={title}
        onChange={(event) => setTitle(event.target.value)}
        onKeyDown={onKeyDown}
        onBlur={() => {
          if (title.trim() === '') setOpen(false);
        }}
      />
      {create.error ? (
        <span
          data-testid={ids.taskError}
          role="alert"
          className="text-[length:var(--text-xs)] text-alert"
        >
          {create.error instanceof ApiRequestError
            ? create.error.message
            : 'Could not add the task.'}
        </span>
      ) : null}
    </div>
  );
}
