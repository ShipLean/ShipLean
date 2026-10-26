import type { AttachmentMarkdownDto } from '../../../api/attachments';
import { ImagesPanel } from './ImagesPanel';
import { NoHistoryNote } from './NoHistoryNote';
import { OutlinePanel } from './OutlinePanel';
import { SiblingsPanel } from './SiblingsPanel';

// The page's right column: outline, images, the no-history note and the item's Markdown files.
export function AttachmentSidePanel({
  projectKey,
  itemKey,
  data,
}: {
  projectKey: string;
  itemKey: string;
  data: AttachmentMarkdownDto;
}) {
  return (
    <aside
      aria-label="About this file"
      className="grid content-start gap-4 border-l border-edge p-4"
    >
      <OutlinePanel outline={data.outline} />
      <ImagesPanel images={data.images} itemKey={itemKey} />
      <NoHistoryNote name={data.attachment.name} />
      <SiblingsPanel
        projectKey={projectKey}
        itemKey={itemKey}
        current={data.attachment}
        siblings={data.siblings}
      />
    </aside>
  );
}
