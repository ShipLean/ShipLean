import type { ReactNode } from 'react';

/** A rail section: an overline, a hint line and the body, or "Nothing here." when it is empty. */
export function RailSection({
  title,
  hint,
  empty,
  testId,
  children,
}: {
  title: string;
  hint: string;
  empty: boolean;
  testId: string;
  children: ReactNode;
}) {
  return (
    <section data-testid={testId} className="grid min-w-0 gap-1.5">
      <div className="text-[length:var(--text-2xs)] font-semibold tracking-wide text-ink-muted uppercase">
        {title}
      </div>
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">{hint}</p>
      {empty ? (
        <span className="text-[length:var(--text-sm)] text-ink-muted">Nothing here.</span>
      ) : (
        <div className="grid grid-cols-[minmax(0,1fr)] gap-1.5 text-[length:var(--text-sm)]">
          {children}
        </div>
      )}
    </section>
  );
}
