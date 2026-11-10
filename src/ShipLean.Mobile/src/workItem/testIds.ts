// The Work item screen's test ids.

// WI1
export const workItemHeaderId = 'wi-header';
export const workItemScrollId = 'wi-scroll';
export const workItemTypeId = 'wi-type';
export const workItemParentId = 'wi-parent';
export const workItemTitleId = 'wi-title';
export const workItemOwnerId = 'wi-owner';
export const statusLineId = 'wi-status';
export const statusTextId = 'wi-status-text';
export const statusVerifiedId = 'wi-status-verified';
export const statusVerifiedById = 'wi-status-verified-by';
export const stillTrueId = 'wi-still-true';
export const newStatusId = 'wi-new-status';

// WI2
export const needsYouSectionId = 'wi-needs-you';
export const needsYouTitleId = 'wi-needs-title';
export const needsYouErrorId = 'wi-needs-error';
export const needsYouRetryId = 'wi-needs-retry';
// One card per open ask, by its label ("D10", "Q1"); a waiting child by its key.
export const needsCardId = (label: string) => `wi-needs-card-${label}`;
export const needsAskTextId = (label: string) => `wi-needs-text-${label}`;
export const needsKindId = (label: string) => `wi-needs-kind-${label}`;
export const needsFromId = (label: string) => `wi-needs-from-${label}`;
export const needsAgentTagId = (label: string) => `wi-needs-agent-${label}`;
export const needsSayId = (label: string) => `wi-needs-say-${label}`;
export const needsBtnId = (label: string, action: 'approve' | 'discuss' | 'reply') =>
  `wi-needs-btn-${label}-${action}`;
export const needsPendingId = (label: string) => `wi-needs-pending-${label}`;
export const needsUndoId = (label: string) => `wi-needs-undo-${label}`;
export const needsRetryAnswerId = (label: string) => `wi-needs-retry-${label}`;
export const needsChildId = (key: string) => `wi-needs-child-${key}`;

// WI3
export const compactProgressId = 'wi-progress-compact';
export const progressSectionId = 'wi-progress';
export const storyTasksId = 'wi-story-tasks';
export const goalDetailsId = 'wi-goal';
export const acceptanceCriteriaId = 'wi-criteria';

// WI4
export const timelineId = 'wi-timeline';

// WI5
export const itemActionsRowId = 'wi-actions-row';
export const changeStateSheetId = 'sheet-change-state';
export const assignSheetId = 'sheet-assign';
