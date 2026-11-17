import { retrosTestIds as ids } from '../../testIds';

/** The › at the right of a phone list row: a real element, so the row shows that the whole of it is a link. */
export function RowChevron() {
  return (
    <span
      aria-hidden="true"
      data-testid={ids.retroRowChevron}
      className="absolute top-1/2 right-2.5 -translate-y-1/2 text-lg leading-none text-ink-muted"
    >
      ›
    </span>
  );
}
