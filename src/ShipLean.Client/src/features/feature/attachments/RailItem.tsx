import { Link } from 'react-router-dom';
import type { AttachmentDto } from '../../../api/attachments';
import { routePath } from '../../../routes';
import { formatSize } from '../../workItem/side/fileSize';
import { featureTestIds as ids } from '../testIds';

const rowClass =
  'flex min-h-7.5 min-w-0 flex-1 items-center gap-1.5 px-2 text-left text-[length:var(--text-sm)] text-ink first:border-t-0 hover:text-signal';

// One attachment, by what opening it does: Markdown goes to its page, an image to the preview, a file downloads.
export function RailItem({
  attachment,
  projectKey,
  itemKey,
  onPreview,
  onRemove,
}: {
  attachment: AttachmentDto;
  projectKey: string;
  itemKey: string;
  onPreview: (attachment: AttachmentDto) => void;
  onRemove: (attachment: AttachmentDto) => void;
}) {
  const content = (
    <>
      <span className="min-w-0 flex-1 truncate">{attachment.name}</span>
      <span
        data-testid={ids.railItemSize}
        className="text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted"
      >
        {formatSize(attachment.sizeBytes)}
      </span>
    </>
  );
  return (
    <li
      data-testid={ids.railItem}
      data-attachment-id={attachment.id}
      className="flex items-center border-t border-edge/60 first:border-t-0 pr-1"
    >
      {attachment.kind === 'markdown' ? (
        <Link
          className={rowClass}
          to={routePath('workItemAttachment', {
            key: projectKey,
            itemKey,
            attachmentId: attachment.id,
          })}
        >
          {content}
        </Link>
      ) : attachment.kind === 'image' ? (
        <button type="button" className={rowClass} onClick={() => onPreview(attachment)}>
          {content}
        </button>
      ) : (
        <a className={rowClass} href={attachment.downloadUrl} download={attachment.name}>
          {content}
        </a>
      )}
      <button
        type="button"
        data-testid={ids.railItemRemove}
        aria-label={`Remove ${attachment.name}`}
        className="h-6 rounded-control px-1.5 text-ink-muted hover:bg-signal-soft focus-visible:outline-2 focus-visible:outline-signal"
        onClick={() => onRemove(attachment)}
      >
        ✕
      </button>
    </li>
  );
}
