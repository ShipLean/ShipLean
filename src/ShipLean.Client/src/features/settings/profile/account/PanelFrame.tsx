import type { ReactNode } from 'react';

/** A titled panel of the account column, in the same frame as the identity panel. */
export function PanelFrame({
  title,
  meta,
  testId,
  children,
}: {
  title: string;
  meta?: string;
  testId: string;
  children: ReactNode;
}) {
  return (
    <section
      aria-label={title}
      data-testid={testId}
      className="rounded-panel border border-edge bg-panel"
    >
      <header className="flex min-h-11 flex-wrap items-center gap-2 border-b border-edge px-3 py-2">
        <h2 className="text-[length:var(--text-md)] font-semibold text-ink">{title}</h2>
        {meta === undefined ? null : (
          <span className="text-[length:var(--text-xs)] text-ink-muted">{meta}</span>
        )}
      </header>
      {children}
    </section>
  );
}
