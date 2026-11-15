import type { AttachmentMarkdownDto } from '@shiplean/core/api/attachments';
import { attachmentMeta } from '../header/attachmentMeta';
import { ImagesPanel } from './ImagesPanel';
import { NoHistoryNote } from './NoHistoryNote';

// What the About this file sheet holds on a phone (D1): the facts, Images in this file and the no-history note.
export function AboutThisFile({ data, itemKey }: { data: AttachmentMarkdownDto; itemKey: string }) {
  return (
    <div className="grid gap-4 pt-1">
      <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
        {attachmentMeta(data.attachment)}
      </p>
      <ImagesPanel images={data.images} itemKey={itemKey} />
      <NoHistoryNote name={data.attachment.name} />
    </div>
  );
}
