import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';
import type { AttachmentMarkdownDto } from '@shiplean/core/api/attachments';
import { Markdown } from '../../../components/Markdown';
import { useProjectByKey } from '../../../realtime/useProjectByKey';
import { useTouchShell } from '../../../touch/useTouchShell';
import { fetchAttachments, workItemAttachmentsKey } from '../../workItem/workItemApi';
import type { AttachmentView } from '../header/ViewToggle';
import { attachmentTestIds as ids } from '../testIds';
import { itemViewerImages } from '../viewer/itemViewerImages';

// The file, rendered (the server's sanitised HTML, image placeholders styled by Markdown) or as its source text.
// At touch a tapped image opens the viewer on all of the item's images (D5), read from the item's attachments.
export function AttachmentBody({
  data,
  view,
}: {
  data: AttachmentMarkdownDto;
  view: Exclude<AttachmentView, 'edit'>;
}) {
  const { touch } = useTouchShell();
  // The same list, under the same key, as the Feature rail and the item page: already cached when the page was reached
  // from them, and kept fresh by their uploads and live events.
  const projectId = useProjectByKey(useParams().key).data?.id;
  const attachments = useQuery({
    queryKey: workItemAttachmentsKey(projectId ?? '', data.workItem.key),
    queryFn: () => fetchAttachments(data.workItem.id),
    enabled: touch && view === 'rendered' && projectId !== undefined,
  });
  const viewerImages = useMemo(
    () => (attachments.data ? itemViewerImages(attachments.data) : undefined),
    [attachments.data],
  );
  return view === 'rendered' ? (
    <article data-testid={ids.body} className="max-w-190 px-5 py-4 leading-relaxed">
      <Markdown html={data.html} imageContext={data.workItem.key} viewerImages={viewerImages} />
    </article>
  ) : (
    <pre
      data-testid={ids.sourceText}
      className="m-4 max-w-215 rounded-control border border-edge bg-sunken p-3 font-[family-name:var(--font-mono)] text-[length:var(--text-sm)] leading-relaxed whitespace-pre-wrap touch:text-[13.5px]"
    >
      {data.markdown}
    </pre>
  );
}
