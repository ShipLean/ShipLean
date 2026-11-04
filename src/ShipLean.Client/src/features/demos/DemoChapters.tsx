import { useEffect, useState } from 'react';
import type { DemoProps } from './DemoProps';
import { subscribeTime } from './playerHandle';
import { demosTestIds as ids } from './testIds';

// The chapter the position is in: the last one that has started.
function currentIndex(starts: number[], seconds: number): number {
  const started = starts.filter((start) => start <= seconds).length;
  return Math.max(started - 1, 0);
}

/** The chapter list. A click moves the player and plays; the chapter the player is in is marked. */
export function DemoChapters({ demo, player }: DemoProps) {
  const { chapters } = demo;
  const [seconds, setSeconds] = useState(0);
  useEffect(() => subscribeTime(player, setSeconds), [player]);
  const current = currentIndex(
    chapters.map((chapter) => chapter.startSeconds),
    seconds,
  );
  return (
    <section aria-label="Chapters" className="min-w-0 rounded-panel border border-edge bg-surface">
      <header className="flex min-h-9 items-center gap-2 border-b border-edge px-3">
        <h2 className="text-[length:var(--text-base)] font-semibold">Chapters</h2>
        <span className="text-[length:var(--text-xs)] text-ink-muted">{chapters.length}</span>
      </header>
      <ol data-testid={ids.demoChapters} className="m-0 max-h-80 list-none overflow-y-auto p-1">
        {chapters.map((chapter, index) => (
          <li key={`${chapter.startSeconds}-${chapter.title}`}>
            <button
              type="button"
              data-testid={ids.demoChapter}
              aria-current={index === current}
              onClick={() => player.seekTo(chapter.startSeconds)}
              className="grid min-h-7.5 w-full grid-cols-[48px_minmax(0,1fr)_auto] items-center gap-2 rounded-control px-2 text-left text-[length:var(--text-sm)] hover:bg-sunken aria-[current=true]:bg-signal-soft aria-[current=true]:font-semibold"
            >
              <span className="text-[length:var(--text-xs)] text-ink-muted tabular-nums">
                {chapter.startLabel}
              </span>
              <span className="truncate">{chapter.title}</span>
              {chapter.featureKey !== null && (
                <span className="font-[family-name:var(--font-mono)] text-[length:var(--text-xs)] text-ink-muted">
                  {chapter.featureKey}
                </span>
              )}
            </button>
          </li>
        ))}
      </ol>
    </section>
  );
}
