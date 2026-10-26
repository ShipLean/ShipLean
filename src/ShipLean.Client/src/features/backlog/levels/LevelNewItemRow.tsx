import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { ApiRequestError } from '../../../api/client';
import type { WorkItemTypeId } from '../../../api/refs';
import { TypeIcon } from '../../../components/TypeIcon';
import { useCreateWorkItem } from '../backlogApi';
import { backlogTestIds as ids } from '../testIds';
import type { LevelBacklogLevel } from './levelColumns';

// The level's own type: Epics hold only Features, so a new item at each level is that level's type.
const typeOf: Record<LevelBacklogLevel, { id: WorkItemTypeId; label: string }> = {
  features: { id: 'feature', label: 'Feature' },
  epics: { id: 'epic', label: 'Epic' },
};

// The inline "+ New work item" row of a Features or Epics backlog: a title, Enter adds it at the top and keeps the row open;
// Esc closes it.
export function LevelNewItemRow({
  level,
  projectId,
  projectKey,
  columnCount,
  onClose,
}: {
  level: LevelBacklogLevel;
  projectId: string;
  projectKey: string;
  columnCount: number;
  onClose: () => void;
}) {
  const type = typeOf[level];
  const [title, setTitle] = useState('');
  const input = useRef<HTMLInputElement>(null);
  const create = useCreateWorkItem(projectId, projectKey);
  useEffect(() => input.current?.focus(), []);

  function add() {
    const text = title.trim();
    if (text === '' || create.isPending) return;
    create.mutate(
      { type: type.id, title: text },
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
          <span
            data-testid={ids.addType}
            data-type={type.id}
            className="inline-flex items-center gap-1.5 text-[length:var(--text-sm)]"
          >
            <TypeIcon type={type.id} />
            {type.label}
          </span>
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
