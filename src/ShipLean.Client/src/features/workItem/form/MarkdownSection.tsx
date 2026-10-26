import { lazy, Suspense, useState } from 'react';
import { fullscreen } from '@uiw/react-md-editor/commands';
import { Markdown } from '../../../components/Markdown';
import { useItemMarkdown } from '../../feature/featureApi';
import { ExternalImageNote } from './description/ExternalImageNote';
import { PasteStatus } from './description/PasteStatus';
import { useExternalImages } from './description/useExternalImages';
import { usePasteImage } from './description/usePasteImage';
import './markdownEditor.css';

// The editor pulls in a markdown parser it only uses for its own preview, which this screen does not show.
// Load it when Edit is opened so the rest of the app does not carry that code.
const MDEditor = lazy(() => import('@uiw/react-md-editor/nohighlight'));

export interface MarkdownSectionIds {
  section: string;
  view: string;
  edit: string;
  input: string;
  preview: string;
  pasting: string;
  pasteError: string;
  externalNote: string;
}

// A markdown field with View and Edit. Edit is the toolbar editor (bold, lists, links, code), 320px tall, writing the
// same markdown string as before. View shows the server's HTML: the item's own while the text is unchanged, otherwise a
// render of the draft through POST /api/markdown/render, with images resolved against `workItemId`'s attachments
// when it is given. The editor's own preview stays off: that preview renders markdown in the browser, including raw
// HTML, and does not know about attachments. With a work item id, pasting an image attaches it (and refreshes
// `attachmentsKey`), and Save asks before the draft's external images go in.
export function MarkdownSection({
  title,
  field,
  emptyText,
  value,
  savedValue,
  savedHtml,
  changed,
  ids,
  workItemId,
  attachmentsKey,
  onChange,
}: {
  title: string;
  field: string;
  emptyText: string;
  value: string;
  savedValue: string;
  savedHtml: string;
  changed: boolean;
  ids: MarkdownSectionIds;
  workItemId?: string;
  attachmentsKey?: readonly unknown[];
  onChange: (markdown: string) => void;
}) {
  const [editing, setEditing] = useState(false);
  const unchanged = value === savedValue;
  const rendered = useItemMarkdown(
    value,
    workItemId,
    !editing && !unchanged && value.trim() !== '',
  );
  const externalImages = useExternalImages(value, !unchanged, workItemId, title);
  const paste = usePasteImage({ workItemId, attachmentsKey, value, onChange });
  const html = unchanged ? savedHtml : (rendered.data?.html ?? '');
  const toggle = (on: boolean) =>
    `px-2.5 text-[length:var(--text-sm)] ${on ? 'bg-signal font-semibold text-signal-ink' : 'bg-panel text-ink hover:bg-signal-soft'}`;
  return (
    <section
      data-testid={ids.section}
      data-field={field}
      className="grid grid-cols-[minmax(0,1fr)] gap-2"
    >
      <header className="flex items-center gap-2">
        <h2 className="flex items-center gap-1 text-[length:var(--text-md)] font-semibold">
          {title}
          {changed ? (
            <span
              role="img"
              aria-label="Unsaved change"
              className="size-1.5 rounded-full bg-signal"
            />
          ) : null}
        </h2>
        <div
          role="group"
          aria-label={`${title} mode`}
          className="ml-auto inline-flex h-6 overflow-hidden rounded-control border border-edge"
        >
          <button
            type="button"
            data-testid={ids.view}
            aria-pressed={!editing}
            className={toggle(!editing)}
            onClick={() => setEditing(false)}
          >
            View
          </button>
          <button
            type="button"
            data-testid={ids.edit}
            aria-pressed={editing}
            className={toggle(editing)}
            onClick={() => setEditing(true)}
          >
            Edit
          </button>
        </div>
      </header>
      {editing ? (
        <Suspense
          fallback={
            <div className="h-[320px] rounded-control border border-edge bg-panel" aria-hidden />
          }
        >
          <MDEditor
            className="work-item-markdown"
            value={value}
            preview="edit"
            height={320}
            overflow={false}
            extraCommands={[fullscreen]}
            textareaProps={{
              'aria-label': `${title} (markdown)`,
              onPaste: paste.onPaste,
              // The editor's prop type has no data-* attributes. The textarea renders this one, which the tests fill.
              ...({ 'data-testid': ids.input } as { 'data-testid': string }),
            }}
            onChange={(next) => onChange(next ?? '')}
          />
        </Suspense>
      ) : value.trim() === '' ? (
        <span className="text-[length:var(--text-sm)] text-ink-muted">{emptyText}</span>
      ) : (
        <div data-testid={ids.preview}>
          <Markdown html={html} />
        </div>
      )}
      <PasteStatus
        pasting={paste.pasting}
        error={paste.error}
        ids={{ pasting: ids.pasting, error: ids.pasteError }}
      />
      <ExternalImageNote sources={externalImages} testId={ids.externalNote} />
    </section>
  );
}
