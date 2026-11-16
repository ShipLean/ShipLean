// The glance rows' leading icons (SHIP-724 D1 design): a hand for Ready to pick up, two people for In progress now.
const SVG_PROPS = {
  width: 18,
  height: 18,
  viewBox: '0 0 18 18',
  fill: 'none',
  stroke: 'currentColor',
  strokeWidth: 1.4,
  strokeLinecap: 'round',
  strokeLinejoin: 'round',
  'aria-hidden': true,
} as const;

export function HandIcon() {
  return (
    <svg {...SVG_PROPS}>
      <path d="M6 9.5V4.2a1 1 0 0 1 2 0V8.5M8 8V3a1 1 0 0 1 2 0v5M10 8V3.8a1 1 0 0 1 2 0V9M12 7.5a1 1 0 0 1 2 0V11a5 5 0 0 1-5 5h-.6a4.5 4.5 0 0 1-3.6-1.8L2.6 11.3a1 1 0 0 1 1.5-1.3L6 11.6" />
    </svg>
  );
}

export function PeopleIcon() {
  return (
    <svg {...SVG_PROPS}>
      <circle cx="6.5" cy="6" r="2.5" />
      <path d="M2 15c0-2.5 2-4.2 4.5-4.2S11 12.5 11 15" />
      <circle cx="12.5" cy="6.5" r="2" />
      <path d="M12.5 10.8c2 0 3.5 1.6 3.5 3.7" />
    </svg>
  );
}
