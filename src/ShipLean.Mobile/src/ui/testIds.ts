// Test ids the primitives set themselves. Primitives that take a testID derive their parts from it:
// TextField `${testID}-hint` and `${testID}-error`; Sheet `${testID}-title`; Button and Row put testID on the
// pressable itself. EmptyState and ErrorState and TopBar take no testID: there is one of each on a screen.
export const uiIds = {
  topBar: 'top-bar',
  topBarTitle: 'top-bar-title',
  emptyState: 'empty-state',
  emptyStateAction: 'empty-state-action',
  errorState: 'error-state',
  errorStateAction: 'error-state-action',
} as const;

export const fieldHintId = (testID: string) => `${testID}-hint`;
export const fieldErrorId = (testID: string) => `${testID}-error`;
export const sheetTitleId = (testID: string) => `${testID}-title`;

// The styled primitives (N7). Fixed ids where there is one on a screen at a time; the rest derive from testID.
export const primitiveIds = {
  tabRootBar: 'tab-root-bar',
  tabRootTile: 'tab-root-tile',
  tabBar: 'tab-bar',
  tabMic: 'tab-mic',
  toast: 'toast',
  toastAction: 'toast-action',
  sheetBackdrop: 'sheet-backdrop',
  skeleton: 'skeleton',
  skeletonRow: 'skeleton-row',
  banner: 'banner',
  progressBar: 'progress-bar',
  progressDone: 'progress-done',
  progressCurrent: 'progress-current',
  progressOther: 'progress-other',
  statePill: 'state-pill',
  healthPill: 'health-pill',
  needsPill: 'needs-pill',
  dock: 'dock',
  dockField: 'dock-field',
  dockMic: 'dock-mic',
  typeChip: 'type-chip',
  avatar: 'avatar',
  needsDecisions: 'needs-decisions',
  needsQuestions: 'needs-questions',
} as const;

// TabBar: the same ids as the shell's simple tabs, so swapping TabBar in keeps the shell specs green.
export const tabId = (id: string) => `tab-${id}`;
export const tabBadgeId = (id: string) => `tab-${id}-badge`;
// Segmented: one id per option, and its count.
export const optionId = (testID: string, value: string) => `${testID}-option-${value}`;
export const optionCountId = (testID: string, value: string) => `${optionId(testID, value)}-count`;
// Chip's count; Banner's action button.
export const countId = (testID: string) => `${testID}-count`;
export const actionId = (testID: string) => `${testID}-action`;
// Icon: one id per name. Chip's colour dot, and its trailing chevron is an Icon.
export const iconId = (name: string) => `icon-${name}`;
export const dotId = (testID: string) => `${testID}-dot`;
