import type { Swatch } from '../../../api/persisted';

/** The names the pickers show for the swatches (the design's), not the token names. */
export const SWATCH_NAMES: Record<Swatch, string> = {
  danger: 'Red',
  warn: 'Amber',
  ok: 'Green',
  info: 'Blue',
  accent: 'Accent',
  muted: 'Grey',
  purple: 'Purple',
};
