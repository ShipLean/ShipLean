import { MoreButton } from '../../../touch/MoreButton';
import type { SheetAction } from '../../../touch/ActionSheet';
import type { ViewerImage } from '../../../touch/imageViewer';

const absolute = (src: string) => new URL(src, window.location.href).href;

/** The viewer's ⋯: Download, Copy link and Open original for the image on screen (a K5 action sheet). */
export function ViewerMoreSheet({ image }: { image: ViewerImage }) {
  const actions: SheetAction[] = [
    {
      id: 'download',
      label: 'Download',
      onSelect: () => {
        const link = document.createElement('a');
        link.href = image.src;
        link.download = image.name;
        link.click();
      },
    },
    {
      id: 'copy-link',
      label: 'Copy link',
      onSelect: () => void navigator.clipboard.writeText(absolute(image.src)),
    },
    {
      id: 'open-original',
      label: 'Open original',
      onSelect: () => void window.open(image.src, '_blank', 'noopener'),
    },
  ];
  return (
    <div className="[&_button]:text-white">
      <MoreButton actions={actions} label={image.name || 'Image'} />
    </div>
  );
}
