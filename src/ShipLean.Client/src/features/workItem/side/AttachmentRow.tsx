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
      className="flex min-h-7.5 items-center gap-1.5 border-t border-edge/60 px-2 text-[length:var(--text-sm)] first:border-t-0"
    >
      <a
        data-testid={ids.attachmentDownload}
        href={attachment.downloadUrl}
        download={attachment.name}
        title={`${attachment.name} · ${by}, ${attachment.createdLabel}`}
        className="min-w-0 flex-1 truncate text-ink hover:text-signal hover:underline"
      >
        {attachment.name}
      </a>
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
      <button
        type="button"
        data-testid={ids.attachmentArchive}
        aria-label={`Remove ${attachment.name}`}
        className="h-6 rounded-control px-1.5 text-ink-muted hover:bg-signal-soft focus-visible:outline-2 focus-visible:outline-signal"
        onClick={onArchive}
      >
        ✕
      </button>
    </li>
  );
}
