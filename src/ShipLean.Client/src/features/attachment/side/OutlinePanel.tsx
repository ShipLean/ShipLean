import type { MarkdownOutlineEntryDto } from '../../../api/markdown';
import { attachmentTestIds as ids } from '../testIds';
import { PanelHeading } from './PanelHeading';

// "On this page": the file's headings, each linking to its heading in the rendered body.
export function OutlinePanel({ outline }: { outline: MarkdownOutlineEntryDto[] }) {
  return (
    <section data-testid={ids.outline} aria-label="On this page" className="grid gap-1">
      <PanelHeading>On this page</PanelHeading>
      <ul className="m-0 grid list-none gap-0.5 p-0 text-[length:var(--text-sm)]">
        {outline.map((entry) => (
          <li key={entry.anchor} style={{ paddingLeft: `${(entry.level - 1) * 12}px` }}>
            <a
              data-testid={ids.outlineEntry}
              href={`#${entry.anchor}`}
              className="block px-2 py-0.5 text-ink no-underline hover:text-signal"
            >
              {entry.text}
            </a>
          </li>
        ))}
      </ul>
    </section>
  );
}
