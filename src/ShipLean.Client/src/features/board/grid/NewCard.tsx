import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { ApiRequestError } from '../../../api/client';
import type { Level } from '../../../routes';
import { useCreateCard } from '../boardApi';
import { boardTestIds as ids } from '../testIds';

const TYPE_NAME: Record<Level, string> = {
  stories: 'User Story',
  features: 'Feature',
  epics: 'Epic',
};

// "+ New" at the top of the first column: a title makes a work item of the board's type, which lands in New.
export function NewCard({
  projectId,
  boardId,
  level,
}: {
  projectId: string;
  boardId: string;
  level: Level;
}) {
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState('');
  const input = useRef<HTMLInputElement>(null);
  const create = useCreateCard(projectId, boardId, level);
  useEffect(() => {
    if (open) input.current?.focus();
  }, [open]);

  function close() {
    setOpen(false);
    setTitle('');
    create.reset();
  }

  function add() {
    const text = title.trim();
    if (text === '' || create.isPending) return;
    create.mutate(text, { onSuccess: close });
  }

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === 'Enter') add();
    else if (event.key === 'Escape') close();
  }

  if (!open) {
    return (
      <button
        type="button"
        data-testid={ids.newCardOpen}
        className="btn-ghost h-[26px] justify-start border border-dashed border-edge bg-transparent px-1.5 text-[length:var(--text-xs)] text-ink-muted"
        onClick={() => setOpen(true)}
      >
        + New {TYPE_NAME[level]}
      </button>
    );
  }
  return (
    <div data-testid={ids.newCardForm} className="grid gap-1">
      <input
        ref={input}
        data-testid={ids.newCardTitle}
        aria-label={`${TYPE_NAME[level]} title`}
        className="field w-full"
        placeholder="Title, then Enter"
        value={title}
        onChange={(event) => setTitle(event.target.value)}
        onKeyDown={onKeyDown}
      />
      <div className="flex items-center gap-1">
        <button type="button" data-testid={ids.newCardSubmit} className="btn-signal" onClick={add}>
          Add
        </button>
        <button type="button" data-testid={ids.newCardCancel} className="btn-ghost" onClick={close}>
          Cancel
        </button>
      </div>
      {create.error ? (
        <span
          data-testid={ids.newCardError}
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
