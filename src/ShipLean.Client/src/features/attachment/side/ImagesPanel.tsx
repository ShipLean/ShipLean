import type { MarkdownImageDto } from '../../../api/markdown';
import { attachmentTestIds as ids } from '../testIds';
import { PanelHeading } from './PanelHeading';

// "Images in this file": each distinct image and whether it is attached to the item or external (never shown).
export function ImagesPanel({ images, itemKey }: { images: MarkdownImageDto[]; itemKey: string }) {
  const shown = images.filter((image) => image.status === 'attached').length;
  return (
    <section data-testid={ids.images} aria-label="Images in this file" className="grid gap-1">
      <PanelHeading count={images.length === 0 ? undefined : `${shown} of ${images.length} shown`}>
        Images in this file
      </PanelHeading>
      {images.length === 0 ? (
        <span className="text-[length:var(--text-sm)] text-ink-muted">No images.</span>
      ) : (
        <ul className="m-0 grid list-none overflow-hidden rounded-md border border-edge bg-surface p-0">
          {images.map((image) => (
            <li
              key={image.src}
              data-testid={ids.imageRow}
              className="flex min-h-7.5 items-center gap-1.5 border-t border-edge/60 px-2 text-[length:var(--text-sm)] first:border-t-0"
            >
              <span
                aria-hidden="true"
                className={image.status === 'attached' ? 'text-ok' : 'text-alert'}
              >
                {image.status === 'attached' ? '✓' : '⊘'}
              </span>
              <span className="min-w-0 flex-1 truncate" title={image.src}>
                {image.src}
              </span>
              <span
                data-testid={ids.imageStatus}
                className="text-[length:var(--text-xs)] text-ink-muted"
              >
                {image.status}
              </span>
            </li>
          ))}
        </ul>
      )}
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Attach the missing image to {itemKey} and change the link to its file name. External images
        never render.
      </p>
    </section>
  );
}
