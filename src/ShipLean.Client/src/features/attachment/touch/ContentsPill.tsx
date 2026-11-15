import { useState } from 'react';
import type { MarkdownOutlineEntryDto } from '@shiplean/core/api/markdown';
import { actionSheetRowClass } from '../../../touch/ActionSheet';
import { Sheet } from '../../../touch/Sheet';
import { attachmentTestIds as ids } from '../testIds';
import { useCurrentHeading } from './useCurrentHeading';

// The floating Contents pill (C14): it names the heading being read and opens On this page, where a pick scrolls to the
// heading and closes the sheet. Fewer than two headings and there is nothing to jump between, so it isn't drawn; a
// caller that shows another view (Source, the editor and its Save bar) doesn't render it either.
export function ContentsPill({ outline }: { outline: MarkdownOutlineEntryDto[] }) {
  const [open, setOpen] = useState(false);
  const current = useCurrentHeading(
    outline.map((entry) => entry.anchor),
    outline.length >= 2,
  );
  if (outline.length < 2) return null;
  const label = outline.find((entry) => entry.anchor === current)?.text ?? outline[0]?.text;

  const jump = (anchor: string) => {
    setOpen(false);
    const target = document.getElementById(anchor);
    if (!target) return;
    target.style.scrollMarginTop = 'calc(var(--mobile-bar-height) + 8px)';
    target.scrollIntoView({ block: 'start' });
  };

  return (
    <>
      <button
        type="button"
        data-testid={ids.contentsPill}
        aria-haspopup="dialog"
        className="fixed bottom-[calc(16px+env(safe-area-inset-bottom))] left-1/2 z-30 inline-flex min-h-[var(--touch-target)] max-w-[calc(100%-32px)] -translate-x-1/2 items-center gap-2 rounded-full border border-edge bg-panel px-4 text-[length:var(--text-base)] text-ink shadow-[0_4px_16px_rgb(0_0_0/0.22)]"
        onClick={() => setOpen(true)}
      >
        <span aria-hidden="true">≡</span>
        <span className="min-w-0 truncate">{label}</span>
      </button>
      <Sheet open={open} title="On this page" onClose={() => setOpen(false)}>
        {outline.map((entry) => (
          <button
            key={entry.anchor}
            type="button"
            data-testid={ids.contentsEntry}
            aria-current={entry.anchor === current ? 'location' : undefined}
            className={`${actionSheetRowClass} text-ink aria-[current=location]:bg-signal-soft aria-[current=location]:text-signal`}
            style={{ paddingLeft: `${8 + (entry.level - 1) * 12}px` }}
            onClick={() => jump(entry.anchor)}
          >
            {entry.text}
          </button>
        ))}
      </Sheet>
    </>
  );
}
