import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { ApiRequestError } from '../../../../api/client';
import { inputClass } from '../../../workItem/form/inputClasses';
import { isWebUrl } from '../../../workItem/links/linkTypes';
import { previewLink } from '../../featureApi';
import { featureTestIds as ids } from '../../testIds';
import { LinkPreview } from './LinkPreview';

// The URL, the preview it will get (fetched when the address is pasted or the field is left), Title and Note.
// A preview that fails never blocks Add: the server still adds the link with whatever it can read.
export function UrlTab({
  workItemId,
  url,
  title,
  note,
  error,
  onUrl,
  onTitle,
  onNote,
}: {
  workItemId: string;
  url: string;
  title: string;
  note: string;
  error: string | null;
  onUrl: (value: string) => void;
  onTitle: (value: string) => void;
  onNote: (value: string) => void;
}) {
  const [previewed, setPreviewed] = useState('');
  const preview = useMutation({
    mutationFn: (address: string) => previewLink(workItemId, address),
  });
  const load = (value: string) => {
    const address = value.trim();
    if (address === previewed || !isWebUrl(address)) return;
    setPreviewed(address);
    preview.mutate(address);
  };
  const invalid =
    error ??
    (preview.error instanceof ApiRequestError && preview.error.code === 'link_invalid_url'
      ? preview.error.message
      : null);
  const data = preview.data;
  return (
    <>
      <label className="grid gap-1 text-[length:var(--text-xs)] text-ink-muted">
        URL
        <input
          data-testid={ids.addLinkUrl}
          autoFocus
          placeholder="https://"
          autoComplete="off"
          className={`${inputClass} text-[length:var(--text-sm)]`}
          value={url}
          onChange={(event) => onUrl(event.target.value)}
          onPaste={(event) => load(event.clipboardData.getData('text'))}
          onBlur={(event) => load(event.target.value)}
        />
      </label>
      {invalid ? (
        <span
          data-testid={ids.addLinkError}
          role="alert"
          className="text-[length:var(--text-xs)] text-danger"
        >
          {invalid}
        </span>
      ) : null}
      {preview.isPending ? (
        <span className="text-[length:var(--text-xs)] text-ink-muted">Fetching the preview…</span>
      ) : null}
      {data ? <LinkPreview preview={data} /> : null}
      <div className="grid gap-2 sm:grid-cols-2">
        <label className="grid gap-1 text-[length:var(--text-xs)] text-ink-muted">
          Title
          <input
            data-testid={ids.addLinkTitle}
            autoComplete="off"
            placeholder={data?.title ?? 'Title (optional)'}
            className={inputClass}
            value={title}
            onChange={(event) => onTitle(event.target.value)}
          />
        </label>
        <label className="grid gap-1 text-[length:var(--text-xs)] text-ink-muted">
          Note (one line, optional)
          <input
            data-testid={ids.addLinkNote}
            autoComplete="off"
            placeholder="Why this link matters"
            className={inputClass}
            value={note}
            onChange={(event) => onNote(event.target.value)}
          />
        </label>
      </div>
    </>
  );
}
