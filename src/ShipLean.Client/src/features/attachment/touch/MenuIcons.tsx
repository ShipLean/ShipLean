import type { ReactNode } from 'react';

// The ⋯ sheet's icons: 16px line glyphs in the current colour.
function Glyph({ children }: { children: ReactNode }) {
  return (
    <svg
      viewBox="0 0 16 16"
      width="16"
      height="16"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {children}
    </svg>
  );
}

export const downloadIcon = (
  <Glyph>
    <path d="M8 2v8M4.5 7 8 10.5 11.5 7M3 13.5h10" />
  </Glyph>
);
export const uploadIcon = (
  <Glyph>
    <path d="M8 11V3M4.5 6 8 2.5 11.5 6M3 13.5h10" />
  </Glyph>
);
export const linkIcon = (
  <Glyph>
    <path d="M6.5 9.5a3 3 0 0 0 4.2 0l2-2a3 3 0 0 0-4.2-4.2l-.8.8M9.5 6.5a3 3 0 0 0-4.2 0l-2 2a3 3 0 0 0 4.2 4.2l.8-.8" />
  </Glyph>
);
export const infoIcon = (
  <Glyph>
    <circle cx="8" cy="8" r="6" />
    <path d="M8 7.5V11M8 5.2v.1" />
  </Glyph>
);
