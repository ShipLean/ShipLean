import { useRef, useState, type KeyboardEvent } from 'react';
import { ApiRequestError } from '@shiplean/core/api/client';
import { WORK_ITEM_TYPE_IDS, type WorkItemTypeId } from '@shiplean/core/api/refs';
import { isStoryLevel, typeName } from '@shiplean/core/api/workItemTypes';
import { useCreateOnTaskboard } from '../taskboardApi';
import type { TaskboardScope } from '../taskboardScope';
import { useTouchShell } from '../../../touch/useTouchShell';
import { taskboardTestIds as ids } from '../testIds';

const types = WORK_ITEM_TYPE_IDS.filter(isStoryLevel).map((id) => ({ id, label: typeName(id) }));

/** "+ Add story or bug": creates the item into this sprint and stays open for the next one. Esc closes it on desktop; at touch a ✕ button does. */
export function AddStoryBox({
  projectId,
  scope,
  destination,
  onClose,
}: {
  projectId: string;
  scope: TaskboardScope;
  /** What the helper line names at touch: the Feature key on the Feature board, the sprint name on the Taskboard. */
  destination: string;
  onClose: () => void;
}) {
  const [type, setType] = useState<WorkItemTypeId>('story');
  const [title, setTitle] = useState('');
  const input = useRef<HTMLInputElement>(null);
  const create = useCreateOnTaskboard(projectId, scope);
  const { touch } = useTouchShell();

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
    <div
      data-testid={ids.addStoryBox}
      className="grid grid-cols-[minmax(0,1fr)] gap-1 px-4 py-2 touch:px-3"
    >
      <div className="flex items-center gap-2">
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
              {touch ? option.label.replace('User ', '') : option.label}
            </option>
          ))}
        </select>
        <input
          ref={input}
          autoFocus
          data-testid={ids.addStoryTitle}
          aria-label="Title"
          className="field flex-1 touch:min-w-0"
          placeholder={
            touch
              ? 'Title of the story or bug'
              : scope.kind === 'sprint'
                ? 'Title, then Enter to add to this sprint'
                : 'Title, then Enter to add under this Feature'
          }
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          onKeyDown={onKeyDown}
        />
        <button
          type="button"
          data-testid={ids.addStorySubmit}
          className="btn-signal touch:h-[var(--touch-target)] touch:min-w-[var(--touch-target)]"
          onClick={add}
        >
          Add
        </button>
        {touch ? null : (
          <button
            type="button"
            data-testid={ids.addStoryClose}
            className="btn-ghost"
            onClick={onClose}
          >
            Esc
          </button>
        )}
      </div>
      {touch ? (
        <div className="flex items-center gap-2">
          <p
            data-testid={ids.addStoryHelp}
            className="m-0 min-w-0 flex-1 text-[length:var(--text-xs)] text-ink-muted"
          >
            {scope.kind === 'sprint'
              ? `Adds it to ${destination}, at the bottom. Stays open for the next one.`
              : `Adds it under ${destination}, in the Backlog. Stays open for the next one.`}
          </p>
          <button
            type="button"
            data-testid={ids.addStoryClose}
            className="btn-ghost h-[var(--touch-target)] min-w-[var(--touch-target)] shrink-0"
            aria-label="Close"
            onClick={onClose}
          >
            ✕
          </button>
        </div>
      ) : null}
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
