// The Backlog's test ids.

// B1
export const backlogRowId = (key: string) => `backlog-row-${key}`;
export const backlogRankId = (key: string) => `backlog-rank-${key}`;
export const backlogMoveButtonId = (key: string) => `backlog-move-${key}`;
// "↑ from 4" on a row whose rank changed since the last read.
export const backlogMovedId = (key: string) => `backlog-moved-${key}`;
export const backlogNeedsBarId = (key: string) => `backlog-needs-bar-${key}`;
// "2 decisions · 1 question" on a row that waits on the person (the needs-me state).
export const backlogAsksId = (key: string) => `backlog-asks-${key}`;
// "3 of 9 stories done" under a Feature's or Epic's progress bar.
export const backlogDoneId = (key: string) => `backlog-done-${key}`;
export const backlogTasksId = (key: string) => `backlog-tasks-${key}`;
export const backlogOwnerId = (key: string) => `backlog-owner-${key}`;
export const backlogParentId = (key: string) => `backlog-parent-${key}`;
export const backlogTypeId = (key: string) => `backlog-type-${key}`;
export const backlogPickerOptionId = (key: string) => `backlog-picker-option-${key}`;

export const backlogIds = {
  list: 'backlog-list',
  loading: 'backlog-loading',
  // Segmented's options are optionId('backlog-segment', 'epics' | 'features' | 'stories').
  segment: 'backlog-segment',
  chipNeedsMe: 'backlog-chip-needs-me',
  chipOpen: 'backlog-chip-open',
  // "Rank order. Hold the mic to move, or tap" and the "7 open Features" header above the rows.
  hint: 'backlog-hint',
  count: 'backlog-count',
  // The Epic chip at Features, the Feature chip at Stories.
  chipParent: 'backlog-chip-parent',
  picker: 'sheet-backlog-picker',
  pickerAll: 'backlog-picker-all',
  // The screen's own "Nothing matches this filter" / "Nothing to show" and error use uiIds.
} as const;

// B2
export const moveSheetId = 'sheet-move';
// The row being moved: "SHIP-906 · rank 4 of 7 Features".
export const moveItemId = 'move-item';
export type MoveChoice = 'top' | 'bottom' | 'up' | 'down' | 'other';
export const moveOptionId = (choice: MoveChoice) => `move-option-${choice}`;
// The rank a choice lands on ("to 3"), or why it is off ("Already at the top").
export const moveOptionToId = (choice: MoveChoice) => `move-option-${choice}-to`;
export const moveFilteredId = 'move-filtered';
// Segmented's options are optionId('move-side', 'above' | 'below').
export const moveSideId = 'move-side';
export const movePickId = (key: string) => `move-pick-${key}`;
export const movePreviewId = 'move-preview';
export const movePreviewRowId = (key: string) => `move-preview-row-${key}`;
// "4 → 2" on a preview row.
export const movePreviewPosId = (key: string) => `move-preview-pos-${key}`;
export const moveWarningId = 'move-warning';
export const moveApplyId = 'move-apply';
export const moveBackId = 'move-back';

// B3
export const addRowId = 'backlog-add-row';
export const addRowLabelId = 'backlog-add-label';
export const addRowInputId = 'backlog-add-input';
// Segmented's options are optionId('backlog-add-type', 'story' | 'bug'); only at the Stories level.
export const addRowTypeId = 'backlog-add-type';
export const addRowErrorId = 'backlog-add-error';
