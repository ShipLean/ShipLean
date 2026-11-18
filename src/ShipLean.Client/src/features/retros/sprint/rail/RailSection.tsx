import type { ReactNode } from 'react';

/**
 * A rail section: an overline, a hint line and the body, or "Nothing here." when it is empty. In a sheet (`sheet`) the
 * sheet's title already names it, so the overline goes and the hint becomes a wrapping description line.
 */
export function RailSection({
  title,
  hint,
  empty,
  testId,
  sheet = false,
  note,
  children,
}: {
  title: string;
  hint: string;
  empty: boolean;
  testId: string;
  sheet?: boolean;
  // A second wrapping line after the hint, in a sheet only.
  note?: string;
  children: ReactNode;
}) {
  return (
    <section data-testid={testId} className={`grid min-w-0 ${sheet ? 'gap-2' : 'gap-1.5'}`}>
      {sheet ? null : (
        <div className="text-[length:var(--text-2xs)] font-semibold tracking-wide text-ink-muted uppercase">
          {title}
        </div>
      )}
      <p
        className={`m-0 text-ink-muted ${sheet ? 'px-2 text-[length:var(--text-sm)]' : 'text-[length:var(--text-xs)]'}`}
      >
        {hint}
      </p>
      {sheet && note ? (
        <p className="m-0 px-2 text-[length:var(--text-sm)] text-ink-muted">{note}</p>
      ) : null}
      {empty ? (
        <span className="text-[length:var(--text-sm)] text-ink-muted">Nothing here.</span>
      ) : (
        <div
          className={`grid grid-cols-[minmax(0,1fr)] text-[length:var(--text-sm)] ${sheet ? 'text-[length:var(--text-base)]' : 'gap-1.5'}`}
        >
          {children}
        </div>
      )}
    </section>
  );
}

/** The row class of a sheet's list: 40px at least, a divider under each. */
export const sheetRowClass = 'min-h-10 border-b border-edge px-2 py-1';
