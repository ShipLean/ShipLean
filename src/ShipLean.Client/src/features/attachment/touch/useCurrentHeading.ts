import { useEffect, useState } from 'react';

// A heading whose top has reached this line (below the top bar) is the one being read.
export const HEADING_LINE_PX = 96;

export interface HeadingTop {
  anchor: string;
  top: number;
}

/** The last heading, in document order, whose top has reached the line; the first heading before any has. */
export function currentAnchor(tops: readonly HeadingTop[], line: number): string | null {
  let current = tops[0]?.anchor ?? null;
  for (const heading of tops) {
    if (heading.top <= line) current = heading.anchor;
  }
  return current;
}

/** The anchor of the heading in view (Contents pill, C14), kept current as any scroller moves. Null while `enabled` is false. */
export function useCurrentHeading(anchors: readonly string[], enabled: boolean): string | null {
  const [current, setCurrent] = useState<string | null>(null);
  const key = anchors.join('\n');

  useEffect(() => {
    if (!enabled) return undefined;
    const list = key === '' ? [] : key.split('\n');
    const update = () => {
      const tops = list.flatMap((anchor) => {
        const element = document.getElementById(anchor);
        return element ? [{ anchor, top: element.getBoundingClientRect().top }] : [];
      });
      setCurrent(currentAnchor(tops, HEADING_LINE_PX));
    };
    update();
    // Capture: a scroll does not bubble, and the page may scroll in the shell's main or in the window.
    document.addEventListener('scroll', update, { capture: true, passive: true });
    window.addEventListener('resize', update);
    return () => {
      document.removeEventListener('scroll', update, { capture: true });
      window.removeEventListener('resize', update);
    };
  }, [key, enabled]);

  return enabled ? current : null;
}
