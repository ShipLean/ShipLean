import type { ReactNode } from 'react';

// A warning above the consent buttons: warn-toned, as the design asks for anything the person should think twice about.
export function WarnCallout({ testId, children }: { testId: string; children: ReactNode }) {
  return (
    <div
      data-testid={testId}
      role="alert"
      className="rounded-sm border border-edge px-3 py-2 text-[length:var(--text-sm)]"
      style={{
        borderLeft: '3px solid var(--color-warn)',
        background: 'color-mix(in srgb, var(--color-warn) 9%, var(--color-surface))',
      }}
    >
      {children}
    </div>
  );
}
