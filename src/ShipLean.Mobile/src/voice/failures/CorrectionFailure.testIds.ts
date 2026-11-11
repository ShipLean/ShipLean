export const correctionFailureId = 'voice-failure-correction';
export const correctionFailureSheetId = 'sheet-voice-failure-correction';
export const correctionFailureLeadId = 'voice-failure-correction-lead';
export const correctionFailureDetailId = 'voice-failure-correction-detail';
export const correctionFailureNothingChangedId = 'voice-failure-correction-nothing-changed';
export const correctionFailureFixId = 'voice-failure-correction-fix';
export const correctionFailureWhatCanISayId = 'voice-failure-correction-what-can-i-say';
export const correctionFailureYesId = 'voice-failure-correction-yes';
export const correctionFailureOpenWebId = 'voice-failure-correction-open-web';

// The two closest items of a no match, by position from 0.
export const correctionClosestId = (index: number) => `voice-failure-correction-closest-${index}`;
// A "which one" candidate, by position from 0 (rank order).
export const correctionCandidateId = (index: number) =>
  `voice-failure-correction-candidate-${index}`;
// A state chip, by the state's name.
export const correctionStateId = (state: string) => `voice-failure-correction-state-${state}`;
