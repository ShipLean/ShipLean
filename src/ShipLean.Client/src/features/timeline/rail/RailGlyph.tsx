import type { ReactNode } from 'react';
import { timelineTestIds as ids } from '../testIds';

export type RailGlyphKind = 'bar' | 'review' | 'bracket' | 'arrow' | 'critical' | 'noStart';

const shapes: Record<RailGlyphKind, ReactNode> = {
  bar: <rect x="1" y="3" width="24" height="8" rx="2" fill="var(--chart-completed)" />,
  review: (
    <>
      <rect x="1" y="3" width="10" height="8" rx="2" fill="var(--chart-completed)" />
      <line x1="11" x2="20" y1="7" y2="7" stroke="var(--color-muted)" strokeDasharray="2 2" />
      <circle cx="20" cy="7" r="4.5" fill="var(--color-ok)" />
    </>
  ),
  bracket: <path d="M2 11V5H24V11" fill="none" stroke="var(--tl-badge-story)" strokeWidth="2.5" />,
  arrow: (
    <>
      <path d="M1 3H6V11H20" fill="none" stroke="var(--color-muted)" strokeWidth="1.25" />
      <path d="M20 7L25 11L20 15Z" fill="var(--color-muted)" />
    </>
  ),
  critical: (
    <rect
      x="2"
      y="3"
      width="22"
      height="8"
      rx="2"
      fill="none"
      stroke="var(--color-text)"
      strokeWidth="1.75"
    />
  ),
  noStart: (
    <path d="M13 1L19 7L13 13L7 7Z" fill="none" stroke="var(--color-text)" strokeWidth="1.5" />
  ),
};

/** The 26 by 14 mini picture beside a "Reading the chart" entry. */
export function RailGlyph({ kind }: { kind: RailGlyphKind }) {
  return (
    <svg
      data-testid={ids.railGlyph}
      data-kind={kind}
      width="26"
      height="14"
      viewBox="0 0 26 14"
      aria-hidden="true"
      className="flex-none"
    >
      {shapes[kind]}
    </svg>
  );
}
