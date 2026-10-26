import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { ApiRequestError } from '../../../api/client';
import type { WorkItemTypeId } from '../../../api/refs';
import { useCreateWorkItem } from '../backlogApi';
import { backlogTestIds as ids } from '../testIds';

const types: { id: WorkItemTypeId; label: string }[] = [
  { id: 'story', label: 'User Story' },
  { id: 'bug', label: 'Bug' },
];

// The inline "+ New work item" row: a type and a title. Enter adds the item (the store puts it first, with the next key)
// and keeps the row open; Esc closes it.
export function NewItemRow({
  projectId,
  projectKey,
  columnCount,
  onClose,
}: {
  projectId: string;
  projectKey: string;
  columnCount: number;
  onClose: () => void;
}) {
  const [type, setType] = useState<WorkItemTypeId>('story');
  const [title, setTitle] = useState('');
  const input = useRef<HTMLInputElement>(null);
  const create = useCreateWorkItem(projectId, projectKey);
  useEffect(() => input.current?.focus(), []);

  function add() {
    const text = title.trim();
    if (text === '' || create.isPending) return;
    create.mutate(
      { type, title: text },
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
    <tr data-testid={ids.addRow} className="h-11 bg-signal-soft">
      <td colSpan={columnCount} className="px-2">
        <div className="flex items-center gap-[var(--space-2)]">
          <select
            data-testid={ids.addType}
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
            data-testid={ids.addTitle}
            aria-label="Title"
            className="field flex-1"
            placeholder="Title, then Enter to add at the top"
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            onKeyDown={onKeyDown}
          />
          <button type="button" data-testid={ids.addSubmit} className="btn-signal" onClick={add}>
            Add
          </button>
          <button type="button" data-testid={ids.addClose} className="btn-ghost" onClick={onClose}>
            Esc
          </button>
          {create.error ? (
            <span
              data-testid={ids.addError}
              role="alert"
              className="text-[length:var(--text-sm)] text-alert"
            >
              {create.error instanceof ApiRequestError
                ? create.error.message
                : 'Could not add the item.'}
            </span>
          ) : null}
        </div>
      </td>
    </tr>
  );
}
