// The glyphs of a sprint's ⋯ sheet on a phone (SHIP-1807 P6): 16px line icons drawn in the text colour.
const PATHS = {
  backlog: 'M3 4h10M3 8h10M3 12h10',
  taskboard: 'M2.5 3.5h11M3.5 3.5v8M7 3.5v5M10.5 3.5v6.5',
  capacity: 'M8 2.5a5.5 5.5 0 1 0 5.5 5.5H8z',
  retro: 'M3 8a5 5 0 1 0 1.5-3.5M3 2.5v2.8h2.8',
  demo: 'M2.5 3h11v8h-11zM5.5 13.5h5M7 5.5v3l2.5-1.5z',
  edit: 'M10.5 3l2.5 2.5L6 12.5H3.5V10z',
  close: 'M2.5 4h11v8h-11zM5.5 8h5',
} as const;

export type SprintSheetIconName = keyof typeof PATHS;

export function SprintSheetIcon({ name }: { name: SprintSheetIconName }) {
  return (
    <svg
      viewBox="0 0 16 16"
      className="size-4"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d={PATHS[name]} />
    </svg>
  );
}
