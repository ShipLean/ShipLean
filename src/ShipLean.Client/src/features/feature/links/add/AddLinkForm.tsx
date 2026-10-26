import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ApiRequestError } from '../../../../api/client';
import { addLinkCard, addWorkItemLink, linkCardsKey } from '../../featureApi';
import { featureTestIds as ids } from '../../testIds';
import { UrlTab } from './UrlTab';
import { WorkItemTab } from './WorkItemTab';
import type { WorkItemLinkType } from './WorkItemTab';

const fieldOf = (failure: Error | null): string | null =>
  failure instanceof ApiRequestError &&
  failure.body !== null &&
  typeof failure.body === 'object' &&
  'field' in failure.body &&
  typeof failure.body.field === 'string'
    ? failure.body.field
    : null;

const tabClass = (active: boolean) =>
  `h-7 border-b-2 px-3 text-[length:var(--text-sm)] font-medium ${
    active ? 'border-signal text-ink' : 'border-transparent text-ink-muted hover:text-ink'
  }`;

// The two ways to add: a URL (previewed before it is added) or a work item (key and link type). Add saves at once.
export function AddLinkForm({
  workItemId,
  projectId,
  itemKey,
  onClose,
}: {
  workItemId: string;
  projectId: string;
  itemKey: string;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<'url' | 'workItem'>('url');
  const [url, setUrl] = useState('');
  const [title, setTitle] = useState('');
  const [note, setNote] = useState('');
  const [key, setKey] = useState('');
  const [type, setType] = useState<WorkItemLinkType>('related');

  const add = useMutation({
    mutationFn: async () => {
      if (tab === 'url') {
        await addLinkCard(workItemId, {
          url: url.trim(),
          title: title.trim() === '' ? undefined : title.trim(),
          note: note.trim() === '' ? undefined : note.trim(),
        });
      } else {
        await addWorkItemLink(workItemId, { targetKey: key.trim().toUpperCase(), type });
      }
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: linkCardsKey(projectId, itemKey) });
      onClose();
    },
  });
  const switchTab = (next: 'url' | 'workItem') => {
    add.reset();
    setTab(next);
  };
  const failure = add.error;
  // An error under the input the server named; one with no field (or an unexpected one) goes under the buttons.
  const fieldName = tab === 'url' ? 'url' : 'targetKey';
  const inline = failure && fieldOf(failure) === fieldName ? failure.message : null;
  const general = failure && inline === null ? failure.message : null;
  const ready = tab === 'url' ? url.trim() !== '' : key.trim() !== '';

  return (
    <form
      data-testid={ids.addLinkForm}
      className="grid gap-2 rounded-panel border border-signal bg-panel p-3"
      onSubmit={(event) => {
        event.preventDefault();
        if (ready) add.mutate();
      }}
    >
      <div role="tablist" className="flex gap-1 border-b border-edge">
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'url'}
          data-testid={ids.addLinkTabUrl}
          className={tabClass(tab === 'url')}
          onClick={() => switchTab('url')}
        >
          URL
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'workItem'}
          data-testid={ids.addLinkTabWorkItem}
          className={tabClass(tab === 'workItem')}
          onClick={() => switchTab('workItem')}
        >
          Work item
        </button>
      </div>
      {tab === 'url' ? (
        <UrlTab
          workItemId={workItemId}
          url={url}
          title={title}
          note={note}
          error={inline}
          onUrl={(value) => {
            add.reset();
            setUrl(value);
          }}
          onTitle={setTitle}
          onNote={setNote}
        />
      ) : (
        <WorkItemTab
          workItemKey={key}
          type={type}
          error={inline}
          onKey={(value) => {
            add.reset();
            setKey(value);
          }}
          onType={setType}
        />
      )}
      {general ? (
        <span
          data-testid={ids.addLinkError}
          role="alert"
          className="text-[length:var(--text-xs)] text-danger"
        >
          {general}
        </span>
      ) : null}
      <div className="flex items-center gap-2">
        <button
          type="submit"
          data-testid={ids.addLinkSubmit}
          disabled={!ready || add.isPending}
          className="h-7 rounded-control border border-signal bg-signal px-3 text-[length:var(--text-sm)] font-medium text-signal-ink focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-signal disabled:opacity-50"
        >
          Add link
        </button>
        <button
          type="button"
          data-testid={ids.addLinkCancel}
          className="h-7 rounded-control px-3 text-[length:var(--text-sm)] text-ink-muted hover:text-ink"
          onClick={onClose}
        >
          Cancel
        </button>
        <span className="ml-auto text-[length:var(--text-xs)] text-ink-muted">
          Previews are fetched by the server: public addresses only, 5 s and 1 MB caps, no cookies.
        </span>
      </div>
    </form>
  );
}
