import { useEffect, useRef, type MouseEvent } from 'react';
import { openImageViewer, type ViewerImage } from '../touch/imageViewer';
import { touchTestIds } from '../touch/testIds';
import { useTouchShell } from '../touch/useTouchShell';
import { drawMermaidDiagrams } from './mermaidDiagrams';
import { componentTestIds } from './testIds';
import './markdown.css';

// The three image states the server's render emits (data-md-image): an image attached to the item shows in a figure
// with its file name below; an external image and a missing one are placeholders and never load anything. A drawn
// Mermaid diagram (data-md-diagram) scrolls sideways when wide; one that could not be drawn gets a caution note.
const imageClasses = [
  '[&_figure[data-md-image=attached]]:my-3 [&_figure[data-md-image=attached]]:grid [&_figure[data-md-image=attached]]:justify-items-start [&_figure[data-md-image=attached]]:gap-1',
  '[&_figure[data-md-image=attached]_img]:max-w-full [&_figure[data-md-image=attached]_img]:rounded-control [&_figure[data-md-image=attached]_img]:border [&_figure[data-md-image=attached]_img]:border-edge',
  '[&_figure[data-md-image=attached]_figcaption]:font-[family-name:var(--font-mono)] [&_figure[data-md-image=attached]_figcaption]:text-[length:var(--text-xs)] [&_figure[data-md-image=attached]_figcaption]:text-ink-muted',
  '[&_[data-md-image=external]]:inline-block [&_[data-md-image=external]]:rounded-control [&_[data-md-image=external]]:border [&_[data-md-image=external]]:border-dashed [&_[data-md-image=external]]:border-edge [&_[data-md-image=external]]:bg-sunken [&_[data-md-image=external]]:px-2 [&_[data-md-image=external]]:text-[length:var(--text-sm)] [&_[data-md-image=external]]:text-ink-muted',
  '[&_[data-md-image=missing]]:inline-block [&_[data-md-image=missing]]:rounded-control [&_[data-md-image=missing]]:border [&_[data-md-image=missing]]:border-dashed [&_[data-md-image=missing]]:border-caution [&_[data-md-image=missing]]:px-2 [&_[data-md-image=missing]]:font-[family-name:var(--font-mono)] [&_[data-md-image=missing]]:text-[length:var(--text-sm)] [&_[data-md-image=missing]]:text-caution',
  '[&_[data-md-diagram]]:my-3 [&_[data-md-diagram]]:overflow-x-auto [&_[data-md-diagram]_svg]:max-w-full',
  '[&_[data-md-diagram-error]]:text-[length:var(--text-sm)] [&_[data-md-diagram-error]]:text-caution',
].join(' ');

const attachedImage = 'figure[data-md-image=attached] img';

// Wraps each table and each code block (not a Mermaid one, which draws itself as a diagram) in a box that scrolls
// sideways at touch, so the page never does (markdown.css). Above 900px the box is `display: contents` and changes
// nothing. A scroll listener keeps `data-more` set while there is more to the right, for the edge fade.
function wrapScrollBoxes(root: HTMLElement): () => void {
  const blocks = root.querySelectorAll<HTMLElement>('table, pre');
  for (const block of blocks) {
    if (block.parentElement?.hasAttribute('data-md-scroll')) continue;
    if (block.querySelector('code.language-mermaid')) continue;
    const box = document.createElement('div');
    box.dataset.mdScroll = '';
    box.setAttribute('data-testid', touchTestIds.markdownScroll);
    block.replaceWith(box);
    box.append(block);
  }
  const cleanups = [...root.querySelectorAll<HTMLElement>('[data-md-scroll]')].map((box) => {
    const update = () => {
      box.toggleAttribute('data-more', box.scrollLeft + box.clientWidth < box.scrollWidth - 1);
    };
    const observer = new ResizeObserver(update);
    observer.observe(box);
    if (box.firstElementChild) observer.observe(box.firstElementChild);
    box.addEventListener('scroll', update, { passive: true });
    update();
    return () => {
      observer.disconnect();
      box.removeEventListener('scroll', update);
    };
  });
  return () => {
    for (const cleanup of cleanups) cleanup();
  };
}

/**
 * Renders HTML that the server produced from markdown (`POST /api/markdown/render`, or a DTO's rendered field),
 * which is sanitised there. This component never parses or sanitises markdown: do not pass it any HTML that did not
 * come from the server's render. It never grows wider than its container: wide tables and code blocks scroll sideways
 * and long words wrap. Fenced ```mermaid blocks are drawn as diagrams once the HTML is in place.
 *
 * At 900px and below the text is 15px, and tapping an attached image opens the image viewer on it. `imageContext`
 * (for example the item key) goes to the viewer; `viewerImages` replaces this Markdown's own images as the set the
 * viewer swipes through, opening on the tapped image's place in it (or the first when it is not there).
 */
export function Markdown({
  html,
  imageContext,
  viewerImages,
}: {
  html: string;
  imageContext?: string;
  viewerImages?: readonly ViewerImage[];
}) {
  const root = useRef<HTMLDivElement>(null);
  const { touch } = useTouchShell();
  useEffect(() => (root.current ? wrapScrollBoxes(root.current) : undefined), [html]);
  useEffect(() => {
    let stale = false;
    if (root.current) void drawMermaidDiagrams(root.current, () => stale);
    return () => {
      stale = true;
    };
  }, [html]);
  const onClick = (event: MouseEvent<HTMLDivElement>) => {
    if (!touch || !(event.target instanceof Element)) return;
    const tapped = event.target.closest<HTMLImageElement>(attachedImage);
    if (!tapped || !root.current?.contains(tapped)) return;
    const own = [...root.current.querySelectorAll<HTMLImageElement>(attachedImage)];
    const toViewerImage = (img: HTMLImageElement): ViewerImage => ({
      src: img.getAttribute('src') ?? '',
      alt: img.alt,
      name: img.closest('figure')?.querySelector('figcaption')?.textContent ?? '',
    });
    const images = viewerImages ?? own.map(toViewerImage);
    const tappedSrc = tapped.getAttribute('src') ?? '';
    const index = viewerImages
      ? viewerImages.findIndex((image) => image.src === tappedSrc)
      : own.indexOf(tapped);
    openImageViewer({ images, start: Math.max(index, 0), context: imageContext });
  };
  return (
    <div
      ref={root}
      data-md-root=""
      onClick={onClick}
      data-testid={componentTestIds.markdown}
      className={`text-[length:var(--text-base)] break-words text-ink [&_a]:text-signal [&_a]:underline [&_blockquote]:border-l-2 [&_blockquote]:border-edge [&_blockquote]:pl-3 [&_blockquote]:text-ink-muted [&_code]:rounded-[var(--radius-xs)] [&_code]:bg-sunken [&_code]:px-1 [&_code]:font-[family-name:var(--font-mono)] [&_h1]:text-[length:var(--text-lg)] [&_h1]:font-semibold [&_h2]:text-[length:var(--text-md)] [&_h2]:font-semibold [&_h3]:font-semibold [&_ol]:list-decimal [&_ol]:pl-5 [&_p]:my-2 [&_pre]:overflow-auto [&_pre]:rounded-control [&_pre]:bg-sunken [&_pre]:p-2 [&_table]:block [&_table]:w-max [&_table]:max-w-full [&_table]:overflow-x-auto [&_table]:border-collapse [&_td]:border [&_td]:border-edge [&_td]:px-2 [&_th]:border [&_th]:border-edge [&_th]:bg-sunken [&_th]:px-2 [&_ul]:list-disc [&_ul]:pl-5 ${imageClasses}`}
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
}
