import { lazy, Suspense } from 'react';
import { fullscreen } from '@uiw/react-md-editor/commands';
import { PasteStatus } from '../../workItem/form/description/PasteStatus';
import { usePasteImage } from '../../workItem/form/description/usePasteImage';
import { workItemAttachmentsKey } from '../../workItem/workItemApi';
import { attachmentTestIds as ids } from '../testIds';
import '../../workItem/form/markdownEditor.css';

// The same toolbar editor as a description, loaded when Edit is first opened.
const MDEditor = lazy(() => import('@uiw/react-md-editor/nohighlight'));

// Edit: the file's Markdown in the toolbar editor, with Save and Cancel. Save uploads the text under the same name, so
// it replaces the file like Replace… does. Pasting an image attaches it to the item and inserts its reference.
export function AttachmentEditor({
  projectId,
  workItemId,
  itemKey,
  name,
  value,
  changed,
  saving,
  error,
  onChange,
  onSave,
  onCancel,
}: {
  projectId: string;
  workItemId: string;
  itemKey: string;
  name: string;
  value: string;
  changed: boolean;
  saving: boolean;
  error: string | null;
  onChange: (markdown: string) => void;
  onSave: () => void;
  onCancel: () => void;
}) {
  const paste = usePasteImage({
    workItemId,
    attachmentsKey: workItemAttachmentsKey(projectId, itemKey),
    value,
    onChange,
  });
  return (
    <div data-testid={ids.editor} className="grid content-start gap-2 px-5 py-4">
      <Suspense
        fallback={
          <div className="h-[560px] rounded-control border border-edge bg-panel" aria-hidden />
        }
      >
        <MDEditor
          className="work-item-markdown"
          value={value}
          preview="edit"
          height={560}
          overflow={false}
          extraCommands={[fullscreen]}
          textareaProps={{
            'aria-label': `${name} (markdown)`,
            onPaste: paste.onPaste,
            // The editor's prop type has no data-* attributes. The textarea renders this one, which the tests fill.
            ...({ 'data-testid': ids.editorInput } as { 'data-testid': string }),
          }}
          onChange={(next) => onChange(next ?? '')}
        />
      </Suspense>
      <PasteStatus
        pasting={paste.pasting}
        error={paste.error}
        ids={{ pasting: ids.editorPasting, error: ids.editorPasteError }}
      />
      {error ? (
        <p
          data-testid={ids.saveError}
          role="alert"
          className="m-0 text-[length:var(--text-sm)] text-alert"
        >
          {error}
        </p>
      ) : null}
      <div className="flex gap-2">
        <button
          type="button"
          data-testid={ids.save}
          disabled={!changed || saving || paste.pasting}
          className="h-7 rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-semibold text-signal-ink disabled:opacity-50"
          onClick={onSave}
        >
          {saving ? 'Saving…' : 'Save'}
        </button>
        <button
          type="button"
          data-testid={ids.cancel}
          disabled={saving}
          className="h-7 rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] disabled:opacity-50"
          onClick={onCancel}
        >
          Cancel
        </button>
      </div>
    </div>
  );
}
