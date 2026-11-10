import type { Theme } from '../theme';

// App-scale sizes from design/mobileapp/app.css, derived from the theme's touch and space tokens.
export const layout = (theme: Theme) => ({
  // --app-tabs: the tab bar above the home indicator (44 + 12 = 56).
  tabBar: theme.touch.control + theme.space[3],
  // The tab bar's middle column, where the mic sits (44 + 24 + 16 = 84).
  micSlot: theme.touch.control + theme.space[5] + theme.space[4],
  // Segmented options and chips draw 32 high and reach 44 with hitSlop.
  compact: theme.touch.control - theme.space[3],
  compactSlop: theme.space[3] / 2,
  // State, health and needs pills (22 high).
  pill: theme.space[5] - 2,
  // Dock mic placeholder (--app-touch + 8 = 52).
  dockMic: theme.touch.bar,
  // A list row's minimum height in the lists (.row: 60).
  listRow: theme.touch.row + theme.space[4],
});

// Hex colour with an alpha byte: the design's colour-mix(<c> 14%, transparent) tints.
export const tint = (hex: string, alpha: number) =>
  `${hex}${Math.round(alpha * 255)
    .toString(16)
    .padStart(2, '0')}`;
