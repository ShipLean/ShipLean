import { useState } from 'react';
import { ApiRequestError } from '../../../api/client';
import { LINK_TYPES, type LinkType } from '../../../api/persisted';
import { inputClass } from '../form/inputClasses';
import { linkSignature, newDraftLink, type DraftLink } from '../form/draft';
import { workItemTestIds as ids } from '../testIds';
import { fetchWorkItem } from '../workItemApi';
import { isWebUrl, linkTypeLabels } from './linkTypes';

const buttonClass =
  'inline-flex h-7 items-center rounded-control border px-3 text-[length:var(--text-sm)] font-medium focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-signal disabled:opacity-50';

// Adds a link, or edits `initial`: a type, then a work item key of this project or a web address. A work item is looked
// up by key when the form is submitted, so the link carries the item's id.
export function LinkForm({
  projectId,
  itemKey,
  others,
  initial,
  onSubmit,
  onCancel,
}: {
  projectId: string;
  itemKey: string;
  others: readonly DraftLink[];
  initial: DraftLink | null;
  onSubmit: (link: DraftLink) => void;
  onCancel: () => void;
}) {
  const [type, setType] = useState<LinkType>(initial?.type ?? 'related');
  const [key, setKey] = useState(initial?.key ?? '');
  const [url, setUrl] = useState(initial?.url ?? '');
  const [title, setTitle] = useState(initial?.title ?? '');
  const [error, setError] = useState<string | null>(null);
  const [checking, setChecking] = useState(false);

  const finish = (value: Omit<DraftLink, 'uid' | 'id'>) => {
    if (others.some((other) => linkSignature(other) === linkSignature(value))) {
      setError('That link is already on this work item.');
      return;
    }
    // Submitting what the link already is changes nothing.
    onSubmit(
      initial && linkSignature(initial) === linkSignature(value) ? initial : newDraftLink(value),
    );
  };

  const submit = async () => {
    setError(null);
    if (type === 'url') {
      const address = url.trim();
      if (!isWebUrl(address)) {
        setError('Enter a full URL starting with https://');
        return;
      }
      finish({
        type,
        workItemId: null,
        key: null,
        url: address,
        title: title.trim() || null,
        card: null,
        autoLinked: null,
      });
      return;
    }
    const wanted = key.trim().toUpperCase();
    if (wanted === '') {
      setError('Enter the key of a work item in this project.');
      return;
    }
    if (wanted === itemKey) {
      setError("A work item can't link to itself.");
      return;
    }
    setChecking(true);
    try {
      const target = await fetchWorkItem(projectId, wanted);
      finish({
        type,
        workItemId: target.id,
        key: target.key,
        url: null,
        title: target.title,
        card: null,
        autoLinked: null,
      });
    } catch (failure) {
      setError(
        failure instanceof ApiRequestError && failure.status === 404
          ? `${wanted} isn't a work item in this project.`
          : (failure as Error).message,
      );
    } finally {
      setChecking(false);
    }
  };

  return (
    <form
      data-testid={ids.linkForm}
      className="grid gap-1.5 rounded-panel border border-signal bg-panel p-2"
      onSubmit={(event) => {
        event.preventDefault();
        void submit();
      }}
    >
      <select
        data-testid={ids.linkType}
        aria-label="Link type"
        className={inputClass}
        value={type}
        onChange={(event) => {
          setType(event.target.value as LinkType);
          setError(null);
        }}
      >
        {LINK_TYPES.map((option) => (
          <option key={option} value={option}>
            {linkTypeLabels[option]}
          </option>
        ))}
      </select>
      {type === 'url' ? (
        <>
          <input
            data-testid={ids.linkUrl}
            aria-label="URL"
            placeholder="https://"
            autoComplete="off"
            className={inputClass}
            value={url}
            onChange={(event) => setUrl(event.target.value)}
          />
          <input
            data-testid={ids.linkTitle}
            aria-label="Link title"
            placeholder="Title (optional)"
            autoComplete="off"
            className={inputClass}
            value={title}
            onChange={(event) => setTitle(event.target.value)}
          />
        </>
      ) : (
        <input
          data-testid={ids.linkKey}
          aria-label="Work item key"
          placeholder="Work item key, such as SHIP-120"
          autoComplete="off"
          className={inputClass}
          value={key}
          onChange={(event) => setKey(event.target.value)}
        />
      )}
      {error ? (
        <span
          data-testid={ids.linkError}
          role="alert"
          className="text-[length:var(--text-xs)] text-alert"
        >
          {error}
        </span>
      ) : null}
      <div className="flex items-center gap-2">
        <button
          type="submit"
          data-testid={ids.linkSubmit}
          disabled={checking}
          className={`${buttonClass} border-signal bg-signal text-signal-ink`}
        >
          {initial ? 'Update link' : 'Add link'}
        </button>
        <button
          type="button"
          data-testid={ids.linkCancel}
          className={`${buttonClass} border-edge bg-panel text-ink hover:bg-signal-soft`}
          onClick={onCancel}
        >
          Cancel
        </button>
        <span className="ml-auto text-[length:var(--text-xs)] text-ink-muted">
          Saved with the form
        </span>
      </div>
    </form>
  );
}
