import type { ReactNode } from 'react';

// A heading in the editor's side panel: small, muted and uppercase (PLACEHOLDERS, SIZE, …).
export function SideHeading({ children }: { children: ReactNode }) {
  return (
    <h4 className="m-0 mb-1 text-[length:var(--text-xs)] font-semibold tracking-[0.04em] text-ink-muted uppercase">
      {children}
    </h4>
  );
}
