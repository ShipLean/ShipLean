import type { AttachmentOutcome } from '../../../api/attachments';
import { Dialog } from '../../../components/Dialog';
import { formatSize } from '../../workItem/side/fileSize';
import { limitsNote } from '../attachments/attachmentGroups';
import { featureTestIds as ids } from '../testIds';

export interface DropEntry {
  file: File;
  outcome: AttachmentOutcome;
  message: string;
  /** Set when the upload of this file was refused. */
  error: string | null;
}

const badge: Record<AttachmentOutcome, string> = {
  add: 'New',
  replace: 'Replace',
  tooLarge: 'Too large',
  overTotal: 'Over the item limit',
};

export const isUploadable = (entry: DropEntry) =>
  entry.outcome === 'add' || entry.outcome === 'replace';

export function DropOverlay({
  itemKey,
  entries,
  uploading,
  onUpload,
  onCancel,
}: {
  itemKey: string;
  entries: DropEntry[];
  uploading: boolean;
  onUpload: () => void;
  onCancel: () => void;
}) {
  const allowed = entries.filter(isUploadable).length;
  return (
    <Dialog
      title={`Drop to attach to ${itemKey}`}
      onClose={onCancel}
      footer={
        <>
          <button
            type="button"
            data-testid={ids.dropCancel}
            className="ml-auto h-7 rounded-control border border-edge px-3 text-[length:var(--text-sm)] text-ink"
            onClick={onCancel}
          >
            Cancel
          </button>
          <button
            type="button"
            data-testid={ids.dropUpload}
            disabled={allowed === 0 || uploading}
            className="h-7 rounded-control bg-signal px-3 text-[length:var(--text-sm)] text-white disabled:opacity-50"
            onClick={onUpload}
          >
            {uploading ? 'Uploading…' : allowed === 1 ? 'Upload 1 file' : `Upload ${allowed} files`}
          </button>
        </>
      }
    >
      <div data-testid={ids.dropOverlay} className="grid gap-2">
        {entries.map((entry) => (
          <div
            key={entry.file.name}
            data-testid={ids.dropOutcome}
            data-outcome={entry.outcome}
            className={`grid gap-0.5 rounded-md border px-3 py-2 ${
              entry.outcome === 'replace' ? 'border-warn bg-warn-soft' : 'border-edge'
            }`}
          >
            <div className="flex items-center gap-2 text-[length:var(--text-sm)]">
              <span className="min-w-0 flex-1 truncate font-mono">{entry.file.name}</span>
              <span className="text-[length:var(--text-xs)] text-ink-muted">
                {formatSize(entry.file.size)}
              </span>
              <span className="rounded-full bg-canvas px-2 text-[length:var(--text-xs)] font-medium text-ink">
                {badge[entry.outcome]}
              </span>
            </div>
            {entry.message ? (
              <span
                data-testid={ids.dropOutcomeMessage}
                className="text-[length:var(--text-xs)] text-ink-muted"
              >
                {entry.message}
              </span>
            ) : null}
            {entry.error ? (
              <span
                data-testid={ids.dropError}
                role="alert"
                className="text-[length:var(--text-xs)] text-alert"
              >
                {entry.error}
              </span>
            ) : null}
          </div>
        ))}
        <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">{limitsNote}</p>
      </div>
    </Dialog>
  );
}
