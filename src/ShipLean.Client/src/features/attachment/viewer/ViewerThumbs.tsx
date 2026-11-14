import type { ViewerImage } from '../../../touch/imageViewer';
import { attachmentTestIds as ids } from '../testIds';

/** The strip of thumbnails under the viewer's image; tapping one shows that image. */
export function ViewerThumbs({
  images,
  current,
  onSelect,
}: {
  images: readonly ViewerImage[];
  current: number;
  onSelect: (index: number) => void;
}) {
  return (
    <div
      data-testid={ids.viewerThumbs}
      className="flex flex-none gap-2 overflow-x-auto px-3 py-2"
      style={{ paddingBottom: 'max(0.5rem, env(safe-area-inset-bottom))' }}
    >
      {images.map((image, index) => (
        <button
          key={`${image.src}-${index}`}
          type="button"
          data-testid={ids.viewerThumb}
          aria-label={`Image ${index + 1}: ${image.name || image.alt}`}
          aria-current={index === current ? 'true' : undefined}
          onClick={() => onSelect(index)}
          className={`size-12 flex-none overflow-hidden rounded-control border-2 bg-black p-0 ${index === current ? 'border-white' : 'border-transparent opacity-60'}`}
        >
          <img src={image.src} alt="" className="size-full object-cover" draggable={false} />
        </button>
      ))}
    </div>
  );
}
