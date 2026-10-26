import type { AttachmentDto, AttachmentKind } from '../../../api/attachments';
import { featureTestIds as ids } from '../testIds';
import { RailItem } from './RailItem';

const groupIds = {
  markdown: ids.railGroupMarkdown,
  image: ids.railGroupImage,
  file: ids.railGroupFile,
} as const;

export function RailGroup({
  kind,
  title,
  hint,
  attachments,
  projectKey,
  itemKey,
  onPreview,
  onRemove,
}: {
  kind: AttachmentKind;
  title: string;
  hint: string;
  attachments: AttachmentDto[];
  projectKey: string;
  itemKey: string;
  onPreview: (attachment: AttachmentDto) => void;
  onRemove: (attachment: AttachmentDto) => void;
}) {
  return (
    <div data-testid={groupIds[kind]} data-kind={kind} className="grid">
      <div className="flex items-center justify-between px-2 pt-2 pb-1 text-[length:var(--text-xs)] text-ink-muted">
        <span className="font-semibold tracking-wide uppercase">{title}</span>
        <span>{hint}</span>
      </div>
      <ul className="m-0 grid list-none p-0">
        {attachments.map((attachment) => (
          <RailItem
            key={attachment.id}
            attachment={attachment}
            projectKey={projectKey}
            itemKey={itemKey}
            onPreview={onPreview}
            onRemove={onRemove}
          />
        ))}
      </ul>
    </div>
  );
}
