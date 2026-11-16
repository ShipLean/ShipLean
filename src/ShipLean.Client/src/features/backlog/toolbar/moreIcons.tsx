import type { ReactNode } from 'react';

// The 18px line glyphs ⋯ More's rows draw in the K1 icon slot (the backlog More sheet design).
const glyph = (paths: ReactNode) => (
  <svg
    viewBox="0 0 24 24"
    width="18"
    height="18"
    fill="none"
    stroke="currentColor"
    strokeWidth="2"
    strokeLinecap="round"
    strokeLinejoin="round"
    className="text-ink-muted"
  >
    {paths}
  </svg>
);

export const searchIcon = glyph(
  <>
    <circle cx="11" cy="11" r="7" />
    <path d="m20 20-3.5-3.5" />
  </>,
);
export const pasteIcon = glyph(
  <>
    <rect x="5" y="4" width="14" height="17" rx="2" />
    <path d="M9 2h6v4H9zM9 11h6M9 15h6" />
  </>,
);
export const selectIcon = glyph(
  <>
    <rect x="4" y="4" width="16" height="16" rx="3" />
    <path d="m8.5 12 2.5 2.5 4.5-5" />
  </>,
);
export const estimateIcon = glyph(<path d="M6 20V10M12 20V4M18 20v-7" />);
