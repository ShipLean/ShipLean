import type { AttachmentMarkdownDto } from '@shiplean/core/api/attachments';
import { attachmentFacts } from '../header/attachmentMeta';
import { attachmentTestIds as ids } from '../testIds';
import { ImagesPanel } from './ImagesPanel';
import { NoHistoryNote } from './NoHistoryNote';

// What the About this file sheet holds on a phone (D1): the facts, Images in this file and the no-history note.
export function AboutThisFile({ data, itemKey }: { data: AttachmentMarkdownDto; itemKey: string }) {
  return (
    <div className="grid gap-4 pt-1">
      <dl
        data-testid={ids.aboutFacts}
        className="m-0 grid grid-cols-[max-content_minmax(0,1fr)] gap-x-6 gap-y-2 text-[length:var(--text-base)]"
      >
        {attachmentFacts(data.attachment, `${data.workItem.key} ${data.workItem.title}`).map(
          ([label, value]) => (
            <div key={label} data-testid={ids.aboutFact} className="contents">
              <dt className="text-ink-muted">{label}</dt>
              <dd className="m-0 min-w-0 break-words">{value}</dd>
            </div>
          ),
        )}
      </dl>
      <ImagesPanel images={data.images} itemKey={itemKey} />
      <NoHistoryNote name={data.attachment.name} />
    </div>
  );
}
