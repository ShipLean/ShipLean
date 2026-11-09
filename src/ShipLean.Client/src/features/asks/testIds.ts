export const asksTestIds = {
  // Section frame and header
  section: 'asks-section',
  loading: 'asks-loading',
  loadFailed: 'asks-load-failed',
  header: 'asks-header',
  count: 'asks-count',
  waitingOn: 'asks-waiting-on',
  summary: 'asks-summary',
  foldToggle: 'asks-fold-toggle',
  empty: 'asks-empty',
  bulkConfirm: 'asks-bulk-confirm',
  // Question rows
  questionRow: 'asks-question-row',
  questionLabel: 'asks-question-label',
  questionState: 'asks-question-state',
  questionReply: 'asks-question-reply',
  questionReplyInput: 'asks-question-reply-input',
  questionReplyPost: 'asks-question-reply-post',
  questionReplyCancel: 'asks-question-reply-cancel',
  questionMore: 'asks-question-more',
  questionWithdraw: 'asks-question-withdraw',
  questionAnswer: 'asks-question-answer',
  questionAnswerLink: 'asks-question-answer-link',
  // Decision rows
  decisionRow: 'asks-decision-row',
  decisionLabel: 'asks-decision-label',
  decisionAnswer: 'asks-decision-answer',
  decisionTag: 'asks-decision-tag',
  decisionState: 'asks-decision-state',
  decisionConfirm: 'asks-decision-confirm',
  decisionChangeButton: 'asks-decision-change-button',
  decisionDiscussButton: 'asks-decision-discuss-button',
  decisionMore: 'asks-decision-more',
  decisionWithdraw: 'asks-decision-withdraw',
  decisionCopyLink: 'asks-decision-copy-link',
  decisionHistoryToggle: 'asks-decision-history-toggle',
  decisionHistory: 'asks-decision-history',
  // Change form (root id is kept by the real DecisionChange)
  decisionChange: 'asks-decision-change',
  changeOption: 'asks-change-option',
  changeOwnInput: 'asks-change-own-input',
  changeNote: 'asks-change-note',
  changeSave: 'asks-change-save',
  changeCancel: 'asks-change-cancel',
  changeSettledWarning: 'asks-change-settled-warning',
  // Discuss form (root id is kept by the real DecisionDiscuss)
  decisionDiscuss: 'asks-decision-discuss',
  discussInput: 'asks-discuss-input',
  discussPost: 'asks-discuss-post',
  discussCancel: 'asks-discuss-cancel',
  // Feature roll-up, History rail and Discussion
  waitingChildRow: 'asks-waiting-child-row',
  historyLine: 'asks-history-line',
  commentAskTag: 'asks-comment-ask-tag',
  // W1
  // W2a
  // W2b
  // W3
} as const;
