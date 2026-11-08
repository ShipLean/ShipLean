import { useState } from 'react';
import { TouchDragGhost } from '../TouchDragGhost';
import { useLongPressDrag } from '../useLongPressDrag';
import { dragHarnessIds as ids } from './dragHarnessIds';

// A scrollable page for useLongPressDrag.phone.spec.tsx: a fixed top bar at z 30 (as the real one), three drop zones of
// cards taller than the screen, and a log of every callback
// at the bottom (so a new line never moves a card). `handle` puts the drag on each card's ⠿ handle only.

const ZONES = ['zone-a', 'zone-b', 'zone-c'] as const;
const PER_ZONE = 5;

type Log = (line: string) => void;

function DragCard({ id, handle, log }: { id: string; handle: boolean; log: Log }) {
  const drag = useLongPressDrag({
    handle,
    onLift: () => log(`lift ${id}`),
    onDrop: (target) => log(`drop ${id} on ${target?.dataset.dropTarget ?? 'nothing'}`),
    onTapWithoutMove: () => log(`tap ${id}`),
    onEdge: (dir) => log(`edge ${dir}`),
  });
  const body = (
    <>
      {handle ? (
        <span
          {...drag.props}
          data-testid={ids.handle}
          className="grid size-8 place-items-center text-ink-muted"
        >
          ⠿
        </span>
      ) : null}
      <span className="flex-1 text-sm">Card {id}</span>
    </>
  );
  return (
    <>
      <li
        {...(handle ? {} : drag.props)}
        data-testid={ids.item}
        data-card={id}
        className={`flex min-h-14 items-center gap-2 rounded-md border border-edge bg-panel px-3 text-ink ${drag.lifted ? 'opacity-40' : ''}`}
      >
        {body}
      </li>
      {drag.lifted ? (
        <TouchDragGhost x={drag.x} y={drag.y}>
          <div className="flex min-h-14 w-80 items-center gap-2 px-3">{body}</div>
        </TouchDragGhost>
      ) : null}
    </>
  );
}

export function DragHarness({ handle = false }: { handle?: boolean }) {
  const [lines, setLines] = useState<string[]>([]);
  const log: Log = (line) => setLines((previous) => [...previous, line]);
  return (
    <div className="min-h-screen bg-canvas text-ink">
      <header
        data-testid={ids.topBar}
        className="fixed inset-x-0 top-0 z-30 flex h-[var(--mobile-bar-height)] items-center border-b border-edge bg-panel px-4 text-sm font-semibold"
      >
        Drag harness
      </header>
      <main className="flex flex-col gap-4 px-4 pt-[calc(var(--mobile-bar-height)+16px)] pb-8">
        {ZONES.map((zone) => (
          <section
            key={zone}
            data-drop-target={zone}
            data-testid={ids.zone}
            className="rounded-lg border border-dashed border-edge p-2"
          >
            <h2 className="pb-2 text-xs font-semibold">{zone}</h2>
            <ul className="flex flex-col gap-2">
              {Array.from({ length: PER_ZONE }, (_, index) => (
                <DragCard key={index} id={`${zone.slice(-1)}${index + 1}`} handle={handle} log={log} />
              ))}
            </ul>
          </section>
        ))}
        <ol data-testid={ids.log} className="text-xs text-ink-muted">
          {lines.map((line, index) => (
            <li key={index} data-testid={ids.logLine}>
              {line}
            </li>
          ))}
        </ol>
      </main>
    </div>
  );
}
