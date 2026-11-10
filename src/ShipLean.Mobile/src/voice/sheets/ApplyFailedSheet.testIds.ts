export const applyFailedSheetId = 'sheet-apply-failed';

export const applyFailedHeadingId = 'voice-apply-failed-heading';
export const applyFailedReasonId = 'voice-apply-failed-reason';
export const applyFailedRetryId = 'voice-apply-failed-retry';
export const applyFailedQueueId = 'voice-apply-failed-queue';
export const applyFailedDiscardId = 'voice-apply-failed-discard';
// Per group: stillApplied, reversed or notSent.
export const applyFailedGroupId = (group: string) => `voice-apply-failed-group-${group}`;
// Per line, by the PreviewLine's id.
export const applyFailedLineId = (id: string) => `voice-apply-failed-line-${id}`;
export const applyFailedMayHaveLandedId = (id: string) =>
  `voice-apply-failed-may-have-landed-${id}`;
