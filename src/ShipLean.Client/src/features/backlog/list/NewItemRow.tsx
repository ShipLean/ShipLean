import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { ApiRequestError } from '@shiplean/core/api/client';
import { WORK_ITEM_TYPE_IDS, type WorkItemTypeId } from '@shiplean/core/api/refs';
import { isStoryLevel, typeName } from '@shiplean/core/api/workItemTypes';
import { useTouchShell } from '../../../touch/useTouchShell';
import { SegmentedChoice } from '../../settings/profile/account/SegmentedChoice';
import { usePasteIntake } from '../add/usePasteIntake';
import { useCreateWorkItem } from '../backlogApi';
import { backlogTestIds as ids } from '../testIds';

const types: { id: WorkItemTypeId; label: string }[] = WORK_ITEM_TYPE_IDS.filter(isStoryLevel).map(
  (id) => ({ id, label: typeName(id) }),
);

// The inline "+ New work item" row: a type and a title. Enter adds the item (the store puts it first, with the next key)
// and keeps the row open; Esc closes it.
export function NewItemRow({
  projectId,
  projectKey,
  columnCount,
  onClose,
  initialType = 'story',
  request = 0,
}: {
  projectId: string;
  projectKey: string;
  columnCount: number;
  onClose: () => void;
  // The type picked when the row opens; a new one (a link to the other type) switches the row to it.
  initialType?: WorkItemTypeId;
  // Counts the requests to open on a type; a new count applies initialType and refocuses, even when the type is the same.
  request?: number;
}) {
  const { touch } = useTouchShell();
  const [type, setType] = useState<WorkItemTypeId>(initialType);
  const [seenRequest, setSeenRequest] = useState(request);
  if (request !== seenRequest) {
    setSeenRequest(request);
    setType(initialType);
  }
  const [title, setTitle] = useState('');
  const input = useRef<HTMLInputElement>(null);
  const create = useCreateWorkItem(projectId, projectKey);
  const paste = usePasteIntake(type);
  useEffect(() => input.current?.focus(), [request]);

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

  const error = create.error ? (
    <span
      data-testid={ids.addError}
      role="alert"
      className="text-[length:var(--text-sm)] text-alert"
    >
      {create.error instanceof ApiRequestError ? create.error.message : 'Could not add the item.'}
    </span>
  ) : null;

  // At touch the form is the design's: a Story / Bug switch, the title and Add, then the help line and Close.
  if (touch) {
    return (
      <div
        data-testid={ids.addRow}
        className="grid gap-2 border-b border-edge bg-signal-soft px-3 py-2.5"
      >
        <div className="flex items-center gap-1.5">
          <div className="w-36 flex-none">
            <SegmentedChoice
              label="Type"
              testId={ids.addType}
              options={types.map((option) => ({
                value: option.id,
                label: option.id === 'story' ? 'Story' : option.label,
              }))}
              selected={type}
              onPick={(value) => setType(value as WorkItemTypeId)}
            />
          </div>
          <input
            ref={input}
            data-testid={ids.addTitle}
            aria-label="Title"
            className="field min-w-0 flex-1 touch:min-h-[var(--touch-target)]"
            placeholder="Title"
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            onKeyDown={onKeyDown}
            {...paste}
          />
          <button
            type="button"
            data-testid={ids.addSubmit}
            className="btn-signal touch:min-h-[var(--touch-target)]"
            onClick={add}
          >
            Add
          </button>
        </div>
        <div className="flex items-center gap-2 text-[length:var(--text-xs)] text-ink-muted">
          <span data-testid={ids.addHelp} className="flex-1">
            Adds at the top with the next key and stays open for the next one.
          </span>
          <button
            type="button"
            data-testid={ids.addClose}
            className="btn-ghost touch:min-h-[var(--touch-target)]"
            onClick={onClose}
          >
            Close
          </button>
        </div>
        {error}
      </div>
    );
  }

  const form = (
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
        {...paste}
      />
      <button type="button" data-testid={ids.addSubmit} className="btn-signal" onClick={add}>
        Add
      </button>
      <button type="button" data-testid={ids.addClose} className="btn-ghost" onClick={onClose}>
        Esc
      </button>
      {error}
    </div>
  );

  return (
    <tr data-testid={ids.addRow} className="h-11 bg-signal-soft">
      <td colSpan={columnCount} className="px-2">
        {form}
      </td>
    </tr>
  );
}
