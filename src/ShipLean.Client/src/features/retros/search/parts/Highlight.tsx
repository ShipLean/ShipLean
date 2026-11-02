import type { ReactNode } from 'react';

/** `text` with every match of `q` (any case) in a `<mark>`, the design's search highlight. */
export function Highlight({ text, q }: { text: string; q: string | undefined }) {
  if (!q) return <>{text}</>;
  const lower = text.toLowerCase();
  const needle = q.toLowerCase();
  const parts: ReactNode[] = [];
  let from = 0;
  for (let at = lower.indexOf(needle); at >= 0; at = lower.indexOf(needle, from)) {
    if (at > from) parts.push(text.slice(from, at));
    parts.push(
      <mark
        key={at}
        className="rounded-[2px] text-inherit"
        style={{ background: 'color-mix(in srgb, var(--color-warn) 32%, transparent)' }}
      >
        {text.slice(at, at + needle.length)}
      </mark>,
    );
    from = at + needle.length;
  }
  parts.push(text.slice(from));
  return <>{parts}</>;
}
