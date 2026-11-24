import type { ReactNode } from 'react';

export type CalloutTone = 'info' | 'warn' | 'purple';

const toneClass: Record<CalloutTone, string> = {
  info: 'border-l-notice bg-[color-mix(in_srgb,var(--color-info)_7%,var(--color-surface))]',
  warn: 'border-l-caution bg-[color-mix(in_srgb,var(--color-warn)_9%,var(--color-surface))]',
  purple:
    'border-l-label-purple bg-[color-mix(in_srgb,var(--color-purple)_7%,var(--color-surface))]',
};

// A left-bordered, tinted note under a prompt's facts (design/screens/prompts.html, .banner).
export function Callout({
  tone,
  testId,
  children,
}: {
  tone: CalloutTone;
  testId?: string;
  children: ReactNode;
}) {
  return (
    <div
      data-testid={testId}
      data-tone={tone}
      className={`grid gap-1 rounded-control border border-l-[3px] border-edge px-3 py-2 text-[length:var(--text-sm)] ${toneClass[tone]}`}
    >
      {children}
    </div>
  );
}
