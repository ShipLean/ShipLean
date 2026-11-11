// App-scale sizes in points, taken from design/mobileapp/app.css. The theme's tokens cover text, touch targets and
// spacing; these are the phone design's own measurements, so each is a named value with its app.css source.
export const layout = {
  // --app-tabs: the tab bar above the home indicator.
  tabBar: 56,
  // .tabs grid-template-columns: the middle column, where the mic sits.
  micSlot: 84,
  // .mic (--app-mic): the raised hold-to-talk button, how far it rises above the bar (top: -26px), its ring
  // (0 0 0 5px surface) and the gap above its label (.mic-label sits 4px above the bar's bottom edge).
  mic: 64,
  micRaise: 26,
  micRing: 5,
  micLabelGap: 3,
  // .tab gap between its icon and label.
  tabGap: 2,
  // .badge on a tab: minimum size, offset from the tab's centre and top, side padding.
  badge: 17,
  badgeTop: 4,
  badgeOffset: 6,
  badgePadding: 5,
  // .seg button and .chip: drawn 32 high, reaching 44 with hitSlop.
  compact: 32,
  compactSlop: 6,
  // .seg padding.
  segmentedPadding: 2,
  // .chip gap.
  chipGap: 6,
  // .state, .health and the needs pill: 22 high.
  pill: 22,
  // .state gap and dot.
  stateGap: 5,
  stateDot: 6,
  // .health dot.
  healthDot: 8,
  // .progress height.
  progress: 6,
  // .dock: field radius, mic size, gap, top padding, extra bottom padding above the home indicator.
  dockFieldRadius: 22,
  dockMic: 52,
  dockGap: 10,
  dockPaddingTop: 10,
  dockPaddingBottom: 4,
  // .toast: height, radius, side padding, action padding.
  toast: 48,
  toastRadius: 12,
  toastPaddingLeft: 14,
  toastPaddingRight: 8,
  toastActionPadding: 10,
  // .toast bottom gap above the tab bar.
  toastGap: 8,
  // .sheet: top radius, grab handle and the gap under it.
  sheetRadius: 20,
  grab: { width: 36, height: 5 },
  grabGap: 12,
  // The project key tile in the tab-root bar: minimum width, height, side padding.
  keyTile: { minWidth: 34, height: 28, padding: 6 },
  // .row: a list row's minimum height.
  listRow: 60,
  // The dock composer: room the closed field keeps for its draft label, the note field's radius over the large
  // radius and its border width, and the faded look of a greyed Command segment.
  dockDraftRoom: 80,
  noteFieldRadiusExtra: 4,
  noteFieldBorder: 1.5,
  disabledOpacity: 0.45,
} as const;

// Hex colour with an alpha byte: the design's colour-mix(<c> 14%, transparent) tints.
export const tint = (hex: string, alpha: number) =>
  `${hex}${Math.round(alpha * 255)
    .toString(16)
    .padStart(2, '0')}`;
