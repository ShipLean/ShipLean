import type { ReactNode } from 'react';

// A side panel's overline heading, with an optional count beside it.
export function PanelHeading({ children, count }: { children: ReactNode; count?: string }) {
  return (
    <div className="flex min-h-5.5 items-center gap-1.5">
      <span className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
        {children}
      </span>
      {count === undefined ? null : (
        <span className="text-[length:var(--text-xs)] text-ink-muted">{count}</span>
      )}
    </div>
  );
}
