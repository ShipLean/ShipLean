export const undoPartialSheetId = 'sheet-voice-undo-partial';
export const undoPartialUndoneId = (pendingId: string) => `voice-undo-partial-undone-${pendingId}`;
export const undoPartialStayedId = (pendingId: string) => `voice-undo-partial-stayed-${pendingId}`;
export const undoPartialRetryId = 'voice-undo-partial-retry';
export const undoPartialDoneId = 'voice-undo-partial-done';
// Change D<n>, and the decision change it opens.
export const undoPartialChangeId = (n: number) => `voice-undo-partial-change-d${n}`;
export const decisionChangeId = 'voice-decision-change';
export const decisionChangeOptionId = (option: string) => `voice-decision-change-option-${option}`;
export const decisionChangeOwnTextId = 'voice-decision-change-own-text';
export const decisionChangeSubmitId = 'voice-decision-change-submit';
export const decisionChangePostedId = 'voice-decision-change-posted';
export const decisionChangeErrorId = 'voice-decision-change-error';
