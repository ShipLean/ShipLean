import { useCallback, useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { touchTestIds } from './testIds';
import { pageInView, scrollTarget } from './pagerScroll';
import './TouchPager.css';

export interface TouchPagerPage {
  id: string;
  label: string;
  count?: number;
  limit?: number;
  /** Change this value to pulse the page's chip once. The pager never turns the page for it. */
  pulse?: number;
}

export interface TouchPagerProps {
  pages: TouchPagerPage[];
  /** The id of the page on screen. */
  current: string;
  onPageChange: (id: string) => void;
  renderPage: (id: string) => ReactNode;
  /** The chip row above the pages; on by default. */
  chips?: boolean;
}

const SETTLE_MS = 120;
const reducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches;

function chipText(page: TouchPagerPage): string | null {
  if (page.count === undefined) return null;
  return page.limit === undefined ? String(page.count) : `${page.count}/${page.limit}`;
}

const isOver = (page: TouchPagerPage) =>
  page.count !== undefined && page.limit !== undefined && page.count > page.limit;

/** Sideways pages that snap, the next one peeking, with a chip row above that turns to a page. */
export function TouchPager({
  pages,
  current,
  onPageChange,
  renderPage,
  chips = true,
}: TouchPagerProps) {
  const trackRef = useRef<HTMLDivElement>(null);
  const chipsRef = useRef<HTMLDivElement>(null);
  const settleTimer = useRef<number>(0);
  const latest = useRef({ pages, current, onPageChange });
  const [pulsing, setPulsing] = useState<ReadonlySet<string>>(new Set());
  const [seenPulses, setSeenPulses] = useState(() => new Map(pages.map((p) => [p.id, p.pulse])));
  const turnedTo = useRef<string | null>(null);

  useLayoutEffect(() => {
    latest.current = { pages, current, onPageChange };
  });

  const geometry = useCallback(() => {
    const track = trackRef.current;
    const first = track?.firstElementChild as HTMLElement | null;
    if (!track || !first) return null;
    const gap = parseFloat(getComputedStyle(track).columnGap) || 0;
    return {
      track,
      gap,
      width: first.offsetWidth,
      max: track.scrollWidth - track.clientWidth,
    };
  }, []);

  const scrollToId = useCallback(
    (id: string, smooth: boolean) => {
      const geo = geometry();
      const index = latest.current.pages.findIndex((page) => page.id === id);
      if (!geo || index < 0) return;
      const left = scrollTarget(index, geo.width, geo.gap, geo.max);
      if (Math.abs(geo.track.scrollLeft - left) < 1) return;
      geo.track.scrollTo({ left, behavior: smooth && !reducedMotion() ? 'smooth' : 'instant' });
    },
    [geometry],
  );

  // Keep the page on screen after the pages change (inserted before it, new counts): restore by id, no animation.
  useLayoutEffect(() => {
    scrollToId(latest.current.current, false);
  }, [pages, scrollToId]);

  // A change of `current` from outside turns to it. One we reported ourselves is already on screen.
  useEffect(() => {
    if (turnedTo.current === current) {
      turnedTo.current = null;
      return;
    }
    scrollToId(current, true);
  }, [current, scrollToId]);

  // The chip on screen stays on screen in its own strip. Scrolls the strip only, never the document.
  useEffect(() => {
    const strip = chipsRef.current;
    const chip = strip?.querySelector<HTMLElement>('[aria-current="true"]');
    if (!strip || !chip) return;
    const left = chip.offsetLeft - strip.offsetLeft;
    const over = left + chip.offsetWidth - (strip.scrollLeft + strip.clientWidth);
    if (left < strip.scrollLeft) strip.scrollLeft = left - 12;
    else if (over > 0) strip.scrollLeft += over + 12;
  }, [current, pages]);

  // A pulse that changes after the first render plays on its chip (state derived from props, during render).
  const changed = pages.filter(
    (page) => !seenPulses.has(page.id) || seenPulses.get(page.id) !== page.pulse,
  );
  if (changed.length > 0) {
    setSeenPulses(new Map(pages.map((p) => [p.id, p.pulse])));
    const started = changed.filter((page) => seenPulses.has(page.id)).map((page) => page.id);
    if (started.length > 0) setPulsing((now) => new Set([...now, ...started]));
  }

  const settle = useCallback(() => {
    const geo = geometry();
    if (!geo) return;
    const { pages: now, current: shown } = latest.current;
    const index = now.findIndex((page) => page.id === shown);
    // Already resting where the current page belongs (including a page the track can't scroll to the start edge).
    if (
      index >= 0 &&
      Math.abs(geo.track.scrollLeft - scrollTarget(index, geo.width, geo.gap, geo.max)) < 2
    )
      return;
    const seen = now[pageInView(geo.track.scrollLeft, geo.width, geo.gap, now.length)];
    if (seen && seen.id !== shown) {
      turnedTo.current = seen.id;
      latest.current.onPageChange(seen.id);
    }
  }, [geometry]);

  useEffect(() => () => window.clearTimeout(settleTimer.current), []);

  const onScroll = () => {
    window.clearTimeout(settleTimer.current);
    settleTimer.current = window.setTimeout(settle, SETTLE_MS);
  };

  const turnTo = (id: string) => {
    if (id !== current) turnedTo.current = id;
    scrollToId(id, true);
    onPageChange(id);
  };

  return (
    <div>
      {chips ? (
        <div ref={chipsRef} className="touch-pager-chips" data-testid={touchTestIds.pagerChips}>
          {pages.map((page) => {
            const text = chipText(page);
            return (
              <button
                key={page.id}
                type="button"
                className={`touch-pager-chip${pulsing.has(page.id) ? ' is-pulsing' : ''}`}
                data-testid={touchTestIds.pagerChip}
                data-page-id={page.id}
                data-over={isOver(page)}
                aria-current={page.id === current ? 'true' : undefined}
                onClick={() => turnTo(page.id)}
                onAnimationEnd={() =>
                  setPulsing((now) => {
                    const next = new Set(now);
                    next.delete(page.id);
                    return next;
                  })
                }
              >
                <span>{page.label}</span>
                {text ? <span className="touch-pager-chip-count">{text}</span> : null}
              </button>
            );
          })}
        </div>
      ) : null}
      <div
        ref={trackRef}
        className="touch-pager"
        data-testid={touchTestIds.pager}
        onScroll={onScroll}
      >
        {pages.map((page) => (
          <div
            key={page.id}
            className="touch-pager-page"
            role="group"
            aria-label={page.label}
            data-testid={touchTestIds.pagerPage}
            data-page-id={page.id}
          >
            {renderPage(page.id)}
          </div>
        ))}
      </div>
    </div>
  );
}
