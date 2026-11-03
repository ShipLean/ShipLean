import { useState } from 'react';
import type { RetroFailureLogDto } from '../../../../api/retros';
import { RetroLogSection } from '../../../retroShared/RetroLogSection';
import { retroSharedTestIds as ids } from '../../../retroShared/testIds';

const sections: { key: keyof RetroFailureLogDto; label: string }[] = [
  { key: 'blockers', label: 'Blockers' },
  { key: 'assumptionsDisproved', label: 'Assumptions disproved' },
  { key: 'friction', label: 'Friction' },
];

/** The selected retro's raw failure log below its board: open when the clicked point matched only there, else collapsed. Nothing is drawn when the log is empty. */
export function SelectedLog({
  log,
  defaultOpen,
}: {
  log: RetroFailureLogDto;
  defaultOpen: boolean;
}) {
  const [open, setOpen] = useState(defaultOpen);
  if (!sections.some((section) => log[section.key].length > 0)) return null;
  return (
    <div className="rounded-sm border border-edge bg-canvas">
      <button
        type="button"
        data-testid={ids.retroCardLogToggle}
        aria-expanded={open}
        className="w-full cursor-pointer px-2.5 py-1.5 text-left text-[length:var(--text-sm)] font-semibold"
        onClick={() => setOpen(!open)}
      >
        {open ? '▾' : '▸'} Raw failure log{' '}
        <span className="font-normal text-ink-muted">· verbatim from the agent</span>
      </button>
      {open ? (
        <dl
          data-testid={ids.retroCardLog}
          className="m-0 grid grid-cols-[120px_minmax(0,1fr)] gap-x-3 gap-y-1.5 px-2.5 pb-2.5 text-[length:var(--text-sm)]"
        >
          {sections.map((section) => (
            <RetroLogSection key={section.key} label={section.label} lines={log[section.key]} />
          ))}
        </dl>
      ) : null}
    </div>
  );
}
