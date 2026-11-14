import type { AttachmentDto } from '@shiplean/core/api/attachments';
import type { ViewerImage } from '../../../touch/imageViewer';

/** Every image attachment of an item, in the order given, as the image viewer's set. `src` is the content URL the rendered Markdown's images use. */
export function itemViewerImages(attachments: readonly AttachmentDto[]): ViewerImage[] {
  return attachments
    .filter((attachment) => attachment.kind === 'image')
    .map((attachment) => ({
      src: attachment.downloadUrl,
      alt: attachment.name,
      name: attachment.name,
    }));
}
