import { useRef, useState, type KeyboardEvent } from 'react';
import { ApiRequestError } from '../../../api/client';
import type { WorkItemTypeId } from '../../../api/refs';
import { useCreateOnTaskboard } from '../taskboardApi';
import type { TaskboardScope } from '../taskboardScope';
import { taskboardTestIds as ids } from '../testIds';

const types: { id: WorkItemTypeId; label: string }[] = [
  { id: 'story', label: 'User Story' },
  { id: 'bug', label: 'Bug' },
];

/** "+ Add story or bug": creates the item into this sprint and stays open for the next one. Esc closes it. */
export function AddStoryBox({
  projectId,
  scope,
  onClose,
}: {
  projectId: string;
  scope: TaskboardScope;
  onClose: () => void;
}) {
  const [type, setType] = useState<WorkItemTypeId>('story');
  const [title, setTitle] = useState('');
  const input = useRef<HTMLInputElement>(null);
  const create = useCreateOnTaskboard(projectId, scope);

  function add() {
    const text = title.trim();
    if (text === '' || create.isPending) return;
    create.mutate(
      { type, title: text, parentId: null },
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
    else if (event.key === 'Escape') onClose();
  }

  return (
    <div data-testid={ids.addStoryBox} className="flex items-center gap-2 px-4 py-2">
      <select
        data-testid={ids.addStoryType}
        aria-label="Type"
        className="field"
        value={type}
        onChange={(event) => setType(event.target.value as WorkItemTypeId)}
        onKeyDown={onKeyDown}
      >
        {types.map((option) => (
          <option key={option.id} value={option.id}>
            {option.label}
          </option>
        ))}
      </select>
      <input
        ref={input}
        autoFocus
        data-testid={ids.addStoryTitle}
        aria-label="Title"
        className="field flex-1"
        placeholder="Title, then Enter to add to this sprint"
        value={title}
        onChange={(event) => setTitle(event.target.value)}
        onKeyDown={onKeyDown}
      />
      <button type="button" data-testid={ids.addStorySubmit} className="btn-signal" onClick={add}>
        Add
      </button>
      <button type="button" data-testid={ids.addStoryClose} className="btn-ghost" onClick={onClose}>
        Esc
      </button>
      {create.error ? (
        <span
          data-testid={ids.addStoryError}
          role="alert"
          className="text-[length:var(--text-sm)] text-alert"
        >
          {create.error instanceof ApiRequestError
            ? create.error.message
            : 'Could not add the item.'}
        </span>
      ) : null}
    </div>
  );
}
