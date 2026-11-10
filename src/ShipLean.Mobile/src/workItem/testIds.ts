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
// One card per open ask, by its label ("D10", "Q1"); a waiting child by its key.
export const needsCardId = (label: string) => `wi-needs-card-${label}`;
export const needsAskTextId = (label: string) => `wi-needs-text-${label}`;
export const needsKindId = (label: string) => `wi-needs-kind-${label}`;
export const needsRecommendedId = (label: string) => `wi-needs-rec-${label}`;
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
export const progressCountsId = 'wi-progress-counts';
export const progressBlockedId = 'wi-progress-blocked';
export const progressLegendId = (part: 'done' | 'current' | 'other' | 'notPlanned') =>
  `wi-progress-legend-${part}`;
export const progressTallyId = (row: 'stories' | 'tasks') => `wi-progress-tally-${row}`;
export const progressErrorId = 'wi-progress-error';
export const progressRetryId = 'wi-progress-retry';
export const storyCountsId = 'wi-story-counts';
export const storyBlockedTaskId = (key: string) => `wi-story-blocked-${key}`;
export const storyBoardNoteId = 'wi-story-board-note';
export const goalTextId = 'wi-goal-text';
export const goalLinksId = 'wi-goal-links';
export const goalToggleId = 'wi-goal-toggle';
export const goalDescriptionId = 'wi-goal-description';
export const criterionId = (index: number) => `wi-criterion-${index}`;

// WI4
export const timelineId = 'wi-timeline';
export const timelineFilterId = 'wi-timeline-filter';
export const timelineEmptyId = 'wi-timeline-empty';
export const timelineErrorId = (half: 'comments' | 'history') => `wi-timeline-error-${half}`;
export const timelineRetryId = (half: 'comments' | 'history') => `wi-timeline-retry-${half}`;
// One entry by its id: a comment's id, "rev-<number>" for a revision, "ask-<kind>-<badge>-<time>" for an ask event.
export const timelineEntryId = (id: string) => `wi-timeline-entry-${id}`;
export const timelineWhoId = (id: string) => `wi-timeline-who-${id}`;
export const timelineAgentTagId = (id: string) => `wi-timeline-agent-${id}`;
export const timelineWhenId = (id: string) => `wi-timeline-when-${id}`;
export const timelineBodyId = (id: string) => `wi-timeline-body-${id}`;
export const timelineEditedId = (id: string) => `wi-timeline-edited-${id}`;
export const timelineSupersededId = (id: string) => `wi-timeline-superseded-${id}`;
export const timelineChangeId = (id: string, index: number) => `wi-timeline-change-${id}-${index}`;

// WI5
export const itemActionsRowId = 'wi-actions-row';
export const changeStateSheetId = 'sheet-change-state';
export const assignSheetId = 'sheet-assign';
