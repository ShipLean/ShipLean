import { useCallback, useLayoutEffect, useState } from 'react';

/** One section a chip jumps to: the section's root is found by its data-testid. */
export interface SectionTarget {
  id: string;
  testId: string;
}

export interface SectionChipsState {
  /** The ids of the sections on the page, in page order. A section that isn't rendered gets no chip. */
  present: readonly string[];
  /** The section being read: the last one whose top has passed the reading line. */
  current: string | null;
  scrollTo: (id: string) => void;
}

// A section counts as being read once its top is above this share of the window's height.
const READING_LINE = 0.25;
// Room between the top bar and a section scrolled to.
const GAP_PX = 8;

const find = (testId: string) => document.querySelector<HTMLElement>(`[data-testid="${testId}"]`);

// The pinned top bar's height (K4): its token, plus the safe area the bar pads with, which this can't read and is 0
// everywhere but a notched phone in portrait.
const barHeight = () =>
  parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--mobile-bar-height')) ||
  0;

function measure(sections: readonly SectionTarget[]) {
  const found = sections.flatMap((section) => {
    const element = find(section.testId);
    return element ? [{ id: section.id, top: element.getBoundingClientRect().top }] : [];
  });
  const line = window.innerHeight * READING_LINE;
  const atBottom =
    window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 2 &&
    window.scrollY > 0;
  let current: string | null = found[0]?.id ?? null;
  for (const section of found) if (section.top <= line) current = section.id;
  // The last sections are often too short to reach the line; at the end of the page the last one is being read.
  if (atBottom && found.length > 0) current = found[found.length - 1]!.id;
  return { present: found.map((section) => section.id), current };
}

const sameState = (
  a: { present: readonly string[]; current: string | null },
  b: { present: readonly string[]; current: string | null },
) => a.current === b.current && a.present.join('|') === b.present.join('|');

/**
 * The section chips' state (SHIP-714 D2 as amended by rule 12): which sections are on the page, which one is being
 * read as the page scrolls, and a scroll to one, clear of the top bar. Sections are listed in page order. Off while
 * `enabled` is false (above 900px there are no chips).
 */
export function useSectionChips(
  sections: readonly SectionTarget[],
  enabled: boolean,
): SectionChipsState {
  const [state, setState] = useState<{ present: readonly string[]; current: string | null }>({
    present: [],
    current: null,
  });

  // Every render: a section can appear or go (a story's Tasks, a Feature's Retros) without the window scrolling.
  useLayoutEffect(() => {
    if (!enabled) return undefined;
    const update = () => {
      const next = measure(sections);
      setState((was) => (sameState(was, next) ? was : next));
    };
    update();
    window.addEventListener('scroll', update, { passive: true });
    window.addEventListener('resize', update);
    return () => {
      window.removeEventListener('scroll', update);
      window.removeEventListener('resize', update);
    };
  });

  const scrollTo = useCallback(
    (id: string) => {
      const section = sections.find((candidate) => candidate.id === id);
      const element = section ? find(section.testId) : null;
      if (!element) return;
      const top = element.getBoundingClientRect().top + window.scrollY - barHeight() - GAP_PX;
      window.scrollTo({ top: Math.max(0, top), behavior: 'smooth' });
    },
    [sections],
  );

  return { present: state.present, current: state.current, scrollTo };
}
