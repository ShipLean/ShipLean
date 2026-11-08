import { Markdown } from '../Markdown';
import { ImageViewerHost } from '../../touch/imageViewer';
import { markdownHarnessIds } from './markdownHarnessIds';

export type MarkdownVariant =
  | 'plain'
  | 'wideTable'
  | 'wideCode'
  | 'wideImage'
  | 'twoImages'
  | 'viewerSet'
  | 'viewerSetMissing'
  | 'mermaid'
  | 'shot';

const picture = (label: string, width: number, color: string) =>
  `data:image/svg+xml;utf8,${encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${Math.round(width / 3)}"><rect width="100%" height="100%" fill="${color}"/><text x="20" y="40" font-size="32" fill="white">${label}</text></svg>`,
  )}`;

const figureOf = (label: string, src: string) =>
  `<figure data-md-image="attached"><img src="${src}" alt="${label} alt"><figcaption>${label}.svg</figcaption></figure>`;
const figure = (label: string, width: number, color: string) =>
  figureOf(label, picture(label, width, color));

const cells = Array.from(
  { length: 12 },
  (_, i) => `<td><code>column_${i}_with_a_long_name</code></td>`,
).join('');
const wideTable = `<table><tbody><tr>${cells}</tr></tbody></table>`;
const wideCode = `<pre><code>${'const veryLongIdentifierName = callSomething(withArguments); '.repeat(6)}</code></pre>`;

const shot = `<h2>Notes</h2><p>Body text at fifteen pixels, with <code>inline code</code> and a <a href="#">link</a>.</p>${wideTable}${wideCode}${figure('Attached', 900, '#3b6ea8')}`;

const set = [
  { src: picture('Set A', 300, '#a83b3b'), alt: 'A from the set', name: 'a.svg' },
  { src: picture('Set B', 300, '#3ba85a'), alt: 'B from the set', name: 'b.svg' },
];

function htmlFor(variant: MarkdownVariant): string {
  switch (variant) {
    case 'wideTable':
      return wideTable;
    case 'wideCode':
      return wideCode;
    case 'wideImage':
      return figure('Wide', 1200, '#3b6ea8');
    case 'twoImages':
      return figure('First', 300, '#a83b3b') + figure('Second', 300, '#3ba85a');
    case 'viewerSet':
      return figureOf('B', set[1]?.src ?? '');
    case 'viewerSetMissing':
      return figure('Other', 300, '#888888');
    case 'mermaid':
      return '<pre><code class="language-mermaid">sequenceDiagram\n    participant U as Person\n    U-&gt;&gt;U: Hello\n</code></pre>';
    case 'shot':
      return shot;
    default:
      return '<p>Plain text</p>';
  }
}

/** CT-only: a Markdown with one of a few fixed bodies, and the image viewer host beside it. */
export function MarkdownTouchHarness({ variant }: { variant: MarkdownVariant }) {
  const viewerImages = variant === 'viewerSet' || variant === 'viewerSetMissing' ? set : undefined;
  return (
    <div data-testid={markdownHarnessIds.ctMarkdownHarness} className="p-4">
      <Markdown html={htmlFor(variant)} imageContext="SHIP-1" viewerImages={viewerImages} />
      <ImageViewerHost />
    </div>
  );
}
