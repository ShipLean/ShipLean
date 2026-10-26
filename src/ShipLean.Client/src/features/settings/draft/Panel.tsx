import type { ReactNode } from 'react';

export function Panel({
  title,
  testId,
  children,
}: {
  title: string;
  testId?: string;
  children: ReactNode;
}) {
  return (
    <section
      aria-label={title}
      data-testid={testId}
      className="mb-4 rounded-panel border border-edge bg-panel"
    >
      <header className="border-b border-edge px-4 py-2">
        <h2 className="text-[length:var(--text-md)] font-semibold text-ink">{title}</h2>
      </header>
      <div className="grid gap-3 p-4">{children}</div>
    </section>
  );
}
