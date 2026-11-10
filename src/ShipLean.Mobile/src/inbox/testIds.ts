// The Inbox's own test ids. Each block belongs to the task named in its comment.
export const inboxIds = {
  // A1a: the row (inboxRowId, inboxAskId, inboxMoreId, inboxBtnId below)
  // A1b: the screen
  // Segmented derives its options: optionId(inboxIds.segment, 'waiting' | 'answered').
  segment: 'inbox-segment',
  list: 'inbox-list',
  empty: 'inbox-empty',
  emptyNew: 'inbox-empty-new',
  today: 'inbox-today',
  kinds: 'inbox-kinds',
  // A2a: the runner's undo toast is UndoToastHost's (toast-undo), not the Inbox's.
  // A2b
  offlineBanner: 'inbox-offline-banner',
  // A3a
  answerSheet: 'sheet-answer',
  // A3b
  reviewSheet: 'sheet-review',
  // A4 (answeredRowId below)
  answeredList: 'answered-list',
} as const;

// A1a
export const inboxRowId = (key: string) => `inbox-row-${key}`;
export const inboxAskId = (key: string, label: string) => `inbox-ask-${key}-${label}`;
export const inboxMoreId = (key: string) => `inbox-more-${key}`;
export const inboxBtnId = (key: string, label: string, action: string) =>
  `inbox-btn-${key}-${label}-${action}`;
// The first ask's label names a batch of decisions ("D1" for D1–D8). Each ask's pending line, kind line and
// who-asked line; the row's age and the item's header (a tap opens the item).
export const inboxPendingId = (key: string, label: string) => `inbox-pending-${key}-${label}`;
export const inboxKindId = (key: string, label: string) => `inbox-kind-${key}-${label}`;
export const inboxFromId = (key: string, label: string) => `inbox-from-${key}-${label}`;
export const inboxAgeId = (key: string) => `inbox-age-${key}`;
export const inboxOpenId = (key: string) => `inbox-open-${key}`;

// A4
export const answeredRowId = (key: string) => `answered-row-${key}`;
