export const voiceSendFailedId = 'voice-send-failed';
// One refused change (a Pending id), and the line saying what of its batch went through.
export const voiceSendFailedRefusedId = (pendingId: string) =>
  `voice-send-failed-refused-${pendingId}`;
export const voiceSendFailedAppliedId = (batchId: string) => `voice-send-failed-applied-${batchId}`;
export const voiceSendFailedDismissId = 'voice-send-failed-dismiss';
