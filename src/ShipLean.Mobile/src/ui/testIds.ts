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
