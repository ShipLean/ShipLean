// The sprint header ⋯ sheet's row icons (sprint-backlog-more design): a flag, a pencil, a list and a tick.
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

export function FlagIcon() {
  return (
    <svg {...SVG_PROPS}>
      <path d="M4 16V2.5M4 3h9l-2 3.5L13 10H4" />
    </svg>
  );
}

export function PencilIcon() {
  return (
    <svg {...SVG_PROPS}>
      <path d="M11.5 3.5l3 3L6 15H3v-3z" />
    </svg>
  );
}

export function ListIcon() {
  return (
    <svg {...SVG_PROPS}>
      <path d="M6.5 4.5H15M6.5 9H15M6.5 13.5H15" />
      <path d="M3 4.5h.01M3 9h.01M3 13.5h.01" strokeWidth={2} />
    </svg>
  );
}

export function TickIcon() {
  return (
    <svg {...SVG_PROPS}>
      <path d="M3.5 9.5l3.5 3.5 7.5-8" />
    </svg>
  );
}
