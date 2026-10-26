import type { ReactNode } from 'react';

const accents = {
  danger: 'var(--color-danger)',
  info: 'var(--color-info)',
  ok: 'var(--color-ok)',
} as const;

// A note above a sign-in form: red for a failure, blue for information, green for "you're signed out".
export function Banner({
  tone,
  testId,
  children,
}: {
  tone: keyof typeof accents;
  testId: string;
  children: ReactNode;
}) {
  const accent = accents[tone];
  return (
    <div
      data-testid={testId}
      data-tone={tone}
      role={tone === 'danger' ? 'alert' : 'status'}
      className="rounded-sm border border-edge px-3 py-2 text-[length:var(--text-sm)]"
      style={{
        borderLeft: `3px solid ${accent}`,
        background: `color-mix(in srgb, ${accent} 9%, var(--color-surface))`,
      }}
    >
      {children}
    </div>
  );
}
