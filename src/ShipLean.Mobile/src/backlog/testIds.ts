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
  // The Epic chip at Features, the Feature chip at Stories.
  chipParent: 'backlog-chip-parent',
  picker: 'sheet-backlog-picker',
  pickerAll: 'backlog-picker-all',
  // The screen's own "Nothing matches this filter" / "Nothing to show" and error use uiIds.
} as const;

// B2
export const moveSheetId = 'sheet-move';

// B3
export const addRowId = 'backlog-add-row';
