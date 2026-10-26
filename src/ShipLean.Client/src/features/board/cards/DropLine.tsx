import { boardTestIds as ids } from '../testIds';

/** The line a dragged card will land on: above a card (over the gap before it), or at the end of a cell. */
export function DropLine({ above = false }: { above?: boolean }) {
  return (
    <span
      data-testid={ids.dropLine}
      aria-hidden
      className={`pointer-events-none h-0.5 rounded-sm ${above ? 'absolute inset-x-0 -top-[5px]' : 'block'}`}
      style={{ background: 'var(--color-accent)' }}
    />
  );
}
