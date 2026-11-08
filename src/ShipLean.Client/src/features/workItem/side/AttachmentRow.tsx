import type { AttachmentDto } from '../../../api/attachments';
import { workItemTestIds as ids } from '../testIds';
import { formatSize } from './fileSize';

// One attachment: a download link, its size, who attached it and when (the label comes from the server), and remove.
export function AttachmentRow({
  attachment,
  onArchive,
}: {
  attachment: AttachmentDto;
  onArchive: () => void;
}) {
  const by = attachment.uploadedBy?.name ?? 'Jira import';
  return (
    <li
      data-testid={ids.attachment}
      data-attachment-id={attachment.id}
      className="flex min-h-7.5 items-center gap-1.5 border-t border-edge/60 px-2 text-[length:var(--text-sm)] first:border-t-0 touch:grid touch:min-h-[var(--touch-target)] touch:grid-cols-[minmax(0,1fr)_auto] touch:gap-x-2 touch:gap-y-0 touch:py-1"
    >
      <a
        data-testid={ids.attachmentDownload}
        href={attachment.downloadUrl}
        download={attachment.name}
        title={`${attachment.name} · ${by}, ${attachment.createdLabel}`}
        className="min-w-0 flex-1 truncate text-ink hover:text-signal hover:underline touch:flex touch:min-h-[var(--touch-target)] touch:items-center touch:whitespace-normal touch:[overflow-wrap:anywhere]"
      >
        {attachment.name}
      </a>
      <span className="contents touch:col-start-1 touch:row-start-2 touch:flex touch:flex-wrap touch:gap-x-2">
        <span
          data-testid={ids.attachmentSize}
          className="text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted"
        >
          {formatSize(attachment.sizeBytes)}
        </span>
        <span
          data-testid={ids.attachmentBy}
          className="text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted"
        >
          {by}, {attachment.createdLabel}
        </span>
      </span>
      <button
        type="button"
        data-testid={ids.attachmentArchive}
        aria-label={`Remove ${attachment.name}`}
        className="h-6 rounded-control px-1.5 text-ink-muted hover:bg-signal-soft focus-visible:outline-2 focus-visible:outline-signal touch:col-start-2 touch:row-span-2 touch:row-start-1 touch:size-[var(--touch-target)]"
        onClick={onArchive}
      >
        ✕
      </button>
    </li>
  );
}
