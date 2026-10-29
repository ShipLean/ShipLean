import { useEffect, useRef } from 'react';
import { drawMermaidDiagrams } from './mermaidDiagrams';
import { componentTestIds } from './testIds';

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

/**
 * Renders HTML that the server produced from markdown (`POST /api/markdown/render`, or a DTO's rendered field),
 * which is sanitised there. This component never parses or sanitises markdown: do not pass it any HTML that did not
 * come from the server's render. It never grows wider than its container: wide tables and code blocks scroll sideways
 * and long words wrap. Fenced ```mermaid blocks are drawn as diagrams once the HTML is in place.
 */
export function Markdown({ html }: { html: string }) {
  const root = useRef<HTMLDivElement>(null);
  useEffect(() => {
    let stale = false;
    if (root.current) void drawMermaidDiagrams(root.current, () => stale);
    return () => {
      stale = true;
    };
  }, [html]);
  return (
    <div
      ref={root}
      data-testid={componentTestIds.markdown}
      className={`text-[length:var(--text-base)] break-words text-ink [&_a]:text-signal [&_a]:underline [&_blockquote]:border-l-2 [&_blockquote]:border-edge [&_blockquote]:pl-3 [&_blockquote]:text-ink-muted [&_code]:rounded-[var(--radius-xs)] [&_code]:bg-sunken [&_code]:px-1 [&_code]:font-[family-name:var(--font-mono)] [&_h1]:text-[length:var(--text-lg)] [&_h1]:font-semibold [&_h2]:text-[length:var(--text-md)] [&_h2]:font-semibold [&_h3]:font-semibold [&_ol]:list-decimal [&_ol]:pl-5 [&_p]:my-2 [&_pre]:overflow-auto [&_pre]:rounded-control [&_pre]:bg-sunken [&_pre]:p-2 [&_table]:block [&_table]:w-max [&_table]:max-w-full [&_table]:overflow-x-auto [&_table]:border-collapse [&_td]:border [&_td]:border-edge [&_td]:px-2 [&_th]:border [&_th]:border-edge [&_th]:bg-sunken [&_th]:px-2 [&_ul]:list-disc [&_ul]:pl-5 ${imageClasses}`}
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
}
