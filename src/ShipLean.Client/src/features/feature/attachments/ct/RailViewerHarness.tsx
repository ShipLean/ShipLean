import { ImageViewerHost } from '../../../../touch/imageViewer';
import { WorkItemFormHarness } from '../../../workItem/ct/WorkItemFormHarness';

/** A Feature page's form slot with the image viewer host the app mounts beside it (CT only). */
export function RailViewerHarness({ itemKey }: { itemKey: string }) {
  return (
    <>
      <WorkItemFormHarness itemKey={itemKey} />
      <ImageViewerHost />
    </>
  );
}
