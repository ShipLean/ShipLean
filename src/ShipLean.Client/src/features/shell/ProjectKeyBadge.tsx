import type { ReactNode } from 'react';

// The project key in its badge: filled for the current project, quiet for the others.
export function ProjectKeyBadge({ children, current }: { children: ReactNode; current: boolean }) {
  return (
    <span
      className={`inline-flex h-5 min-w-[30px] flex-none items-center justify-center rounded-xs px-1 font-mono text-[length:var(--text-2xs)] leading-none font-bold whitespace-nowrap ${
        current ? 'bg-signal text-signal-ink' : 'bg-sunken text-ink'
      }`}
    >
      {children}
    </span>
  );
}
