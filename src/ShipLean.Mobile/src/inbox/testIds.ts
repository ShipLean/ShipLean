// The Inbox's own test ids. Each block belongs to the task named in its comment.
export const inboxIds = {
  // A1a: the row (inboxRowId, inboxAskId, inboxMoreId, inboxBtnId below)
  // A1b: the screen
  segmentWaiting: 'inbox-segment-waiting',
  segmentAnswered: 'inbox-segment-answered',
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

// A4
export const answeredRowId = (key: string) => `answered-row-${key}`;
