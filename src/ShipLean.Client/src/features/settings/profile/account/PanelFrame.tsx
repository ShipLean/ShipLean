import type { ReactNode } from 'react';

/** A titled panel of the account column, in the same frame as the identity panel. */
export function PanelFrame({
  title,
  meta,
  testId,
  action,
  rule = true,
  children,
}: {
  title: string;
  meta?: string;
  testId: string;
  /** A control on the heading's row; at touch it moves to its own row at the right, under the meta line. */
  action?: ReactNode;
  /** The line under the header; a collapsed panel leaves it out. */
  rule?: boolean;
  children: ReactNode;
}) {
  return (
    <section
      aria-label={title}
      data-testid={testId}
      className="rounded-panel border border-edge bg-panel"
    >
      <header
        className={`flex min-h-11 flex-wrap items-center gap-2 px-3 py-2 ${rule ? 'border-b border-edge' : ''}`}
      >
        <h2 className="text-[length:var(--text-md)] font-semibold text-ink">{title}</h2>
        {meta === undefined ? null : (
          <span className="text-[length:var(--text-xs)] text-ink-muted touch:order-2 touch:basis-full touch:text-[length:var(--text-sm)]">
            {meta}
          </span>
        )}
        {action == null ? null : (
          <span className="touch:order-3 touch:ml-auto">{action}</span>
        )}
      </header>
      {children}
    </section>
  );
}
