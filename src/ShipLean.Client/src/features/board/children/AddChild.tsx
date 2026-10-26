import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { ApiRequestError } from '../../../api/client';
import type { Level } from '../../../routes';
import { boardTestIds as ids } from '../testIds';
import { useCreateChild } from './childrenApi';
import { CHILD_TYPES } from './childTypes';

// "+ User story", "+ Bug" or "+ Feature" under the list: a title makes a child of that type under the card.
export function AddChild({
  projectId,
  boardId,
  parentId,
  level,
}: {
  projectId: string;
  boardId: string;
  parentId: string;
  level: Level;
}) {
  const options = CHILD_TYPES[level];
  const [adding, setAdding] = useState<(typeof options)[number] | null>(null);
  const [title, setTitle] = useState('');
  const input = useRef<HTMLInputElement>(null);
  const create = useCreateChild(projectId, boardId, parentId);
  useEffect(() => {
    if (adding) input.current?.focus();
  }, [adding]);

  function close() {
    setAdding(null);
    setTitle('');
    create.reset();
  }

  function add() {
    const text = title.trim();
    if (!adding || text === '' || create.isPending) return;
    create.mutate({ type: adding.type, title: text }, { onSuccess: close });
  }

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === 'Enter') add();
    else if (event.key === 'Escape') {
      event.stopPropagation();
      close();
    }
  }

  if (options.length === 0) return null;
  if (!adding) {
    return (
      <div className="flex gap-0.5 pt-0.5">
        {options.map((option) => (
          <button
            key={option.type}
            type="button"
            data-testid={ids.childAdd}
            data-type={option.type}
            className="btn-ghost h-[22px] border-0 px-1.5 text-[length:var(--text-xs)] text-ink-muted"
            onClick={() => setAdding(option)}
          >
            + {option.label}
          </button>
        ))}
      </div>
    );
  }
  return (
    <div data-testid={ids.childAddForm} data-type={adding.type} className="grid gap-1 pt-1">
      <input
        ref={input}
        data-testid={ids.childAddTitle}
        aria-label={`${adding.label} title`}
        className="field w-full"
        placeholder={`${adding.label} title, then Enter`}
        value={title}
        onChange={(event) => setTitle(event.target.value)}
        onKeyDown={onKeyDown}
      />
      <div className="flex items-center gap-1">
        <button type="button" data-testid={ids.childAddSubmit} className="btn-signal" onClick={add}>
          Add
        </button>
        <button
          type="button"
          data-testid={ids.childAddCancel}
          className="btn-ghost"
          onClick={close}
        >
          Cancel
        </button>
      </div>
      {create.error ? (
        <span
          data-testid={ids.childAddError}
          role="alert"
          className="text-[length:var(--text-xs)] text-alert"
        >
          {create.error instanceof ApiRequestError
            ? create.error.message
            : 'Could not add the item.'}
        </span>
      ) : null}
    </div>
  );
}
