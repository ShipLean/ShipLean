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
export const forecastIcon = glyph(<path d="M3 17l6-6 4 4 8-8M15 7h6v6" />);
export const velocityIcon = glyph(
  <>
    <path d="M4 18a8 8 0 1 1 16 0" />
    <path d="m12 18 4-6" />
  </>,
);
export const createSprintIcon = glyph(<path d="M12 5v14M5 12h14" />);
export const estimateIcon = glyph(<path d="M6 20V10M12 20V4M18 20v-7" />);
export const openIcon = glyph(<path d="M14 4h6v6M20 4l-9 9M18 14v5H5V6h5" />);
export const sprintIcon = glyph(<path d="M4 12a8 8 0 1 0 3-6.2M4 4v5h5" />);
export const parentIcon = glyph(<path d="M6 4v9a2 2 0 0 0 2 2h10M14 11l4 4-4 4" />);
export const moveTopIcon = glyph(<path d="M12 20V5M6 11l6-6 6 6" />);
export const moveBottomIcon = glyph(<path d="M12 4v15M6 13l6 6 6-6" />);
