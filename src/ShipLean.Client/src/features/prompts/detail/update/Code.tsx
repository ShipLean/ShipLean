import type { ReactNode } from 'react';

// Inline code chip, as the mock-up's banner code.
export function Code(props: { children: ReactNode }) {
  return (
    <code className="rounded-sm bg-sunken px-1 font-[family-name:var(--font-mono)] text-[0.92em]">
      {props.children}
    </code>
  );
}
