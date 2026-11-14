import { useState } from 'react';
import { TouchHandle } from '../touch/TouchHandle';
import { touchHandleHarnessIds } from './touchHandleHarnessIds';

// CT-only: a scrollable list of fake rows, each with a TouchHandle in a slot shaped like TouchRow's, and a log of the
// drops. A fixed top bar (z 30) stands in for the real one. The log sits last, so a new line never moves a row.

const IDS = Array.from({ length: 24 }, (_, index) => `r${index + 1}`);

function HarnessRow({
  id,
  rank,
  disabled,
  log,
}: {
  id: string;
  rank: number;
  disabled: boolean;
  log: (line: string) => void;
}) {
  const [lifted, setLifted] = useState(false);
  const body = <span className="flex-1 py-3 pr-3 text-sm font-medium">Row {id}</span>;
  return (
    <li
      data-testid={touchHandleHarnessIds.row}
      data-row-id={id}
      data-drop-target={id}
      className={`flex min-h-[var(--touch-target)] items-stretch border-b border-edge ${lifted ? 'opacity-40' : ''}`}
    >
      <div className="flex w-9 flex-none items-center justify-center self-stretch">
        <TouchHandle
          rowId={id}
          rank={rank}
          disabled={disabled}
          onDrop={(target) => log(`drop ${id} on ${target ?? 'nothing'}`)}
          onLiftedChange={setLifted}
          ghost={<div className="flex min-h-14 w-80 items-stretch">{body}</div>}
        />
      </div>
      {body}
    </li>
  );
}

export function TouchHandleHarness({ disabled = false }: { disabled?: boolean }) {
  const [lines, setLines] = useState<string[]>([]);
  const log = (line: string) => setLines((previous) => [...previous, line]);
  return (
    <div className="min-h-screen bg-canvas text-ink">
      <header
        data-testid={touchHandleHarnessIds.topBar}
        className="fixed inset-x-0 top-0 z-30 flex h-[var(--mobile-bar-height)] items-center border-b border-edge bg-panel px-4 text-sm font-semibold"
      >
        Touch handle harness
      </header>
      <main className="pt-[var(--mobile-bar-height)] pb-8">
        <ul>
          {IDS.map((id, index) => (
            <HarnessRow key={id} id={id} rank={index + 1} disabled={disabled} log={log} />
          ))}
        </ul>
        <ol data-testid={touchHandleHarnessIds.log} className="px-4 pt-4 text-xs text-ink-muted">
          {lines.map((line, index) => (
            <li key={index} data-testid={touchHandleHarnessIds.logLine}>
              {line}
            </li>
          ))}
        </ol>
      </main>
    </div>
  );
}
