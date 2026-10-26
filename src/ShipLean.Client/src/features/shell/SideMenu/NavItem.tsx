import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';

// One link of the side menu: a section (with its icon) or a sub-item under Sprints.
export function NavItem({
  to,
  icon,
  active,
  sub = false,
  testId,
  children,
}: {
  to: string;
  icon?: string;
  active: boolean;
  sub?: boolean;
  testId: string;
  children: ReactNode;
}) {
  const shape = sub
    ? 'h-[26px] pl-8 text-[length:var(--text-sm)] font-normal max-[900px]:hidden'
    : 'h-[30px] font-medium';
  const state = active
    ? sub
      ? 'bg-signal-soft text-ink'
      : 'bg-panel font-semibold text-ink shadow-panel'
    : 'text-ink-muted hover:bg-panel/60 hover:text-ink';
  return (
    <Link
      to={to}
      data-testid={testId}
      aria-current={active ? 'page' : undefined}
      className={`flex items-center gap-2 rounded-control px-2 no-underline ${shape} ${state}`}
    >
      {icon ? (
        <span aria-hidden="true" className="w-4 flex-none text-center">
          {icon}
        </span>
      ) : null}
      {children}
    </Link>
  );
}
