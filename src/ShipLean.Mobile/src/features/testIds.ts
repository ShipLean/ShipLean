// The Features screens' test ids.

// F1
export const featureRowId = (key: string) => `feature-row-${key}`;
export const featureGroupId = (category: string) => `feature-group-${category}`;
export const featureGroupCountId = (category: string) => `feature-group-${category}-count`;
export const featureWhoId = (key: string) => `feature-who-${key}`;
export const featureStatusId = (key: string) => `feature-status-${key}`;
export const featureStaleId = (key: string) => `feature-stale-${key}`;
export const featureRollupId = (key: string) => `feature-rollup-${key}`;
export const featureMovedId = (key: string) => `feature-moved-${key}`;
// The Needs you view: one header per Feature, and the Epic picker's options.
export const featureNeedsHeaderId = (key: string) => `feature-needs-header-${key}`;
export const featureEpicOptionId = (key: string) => `features-epic-option-${key}`;
export const featuresIds = {
  list: 'features-list',
  chipAll: 'features-chip-all',
  chipNeeds: 'features-chip-needs',
  chipRisk: 'features-chip-risk',
  chipBlocked: 'features-chip-blocked',
  chipEpic: 'features-chip-epic',
  picker: 'features-epic-picker',
  pickerAll: 'features-epic-option-all',
  sinceBanner: 'features-since-banner',
  needsEmpty: 'features-needs-empty',
} as const;

// F2
export const featureDetailsId = (key: string) => `feature-details-${key}`;
export const featureNoteId = (key: string) => `feature-note-${key}`;
export const featureDecisionId = (key: string) => `feature-decision-${key}`;
export const featureApproveId = (key: string) => `feature-approve-${key}`;
export const featureDiscussId = (key: string) => `feature-discuss-${key}`;
export const featureHistoryId = (key: string) => `feature-history-${key}`;
export const featureHistoryLineId = (key: string, index: number) =>
  `feature-history-${key}-${index}`;
export const featureHistoryErrorId = (key: string) => `feature-history-error-${key}`;
export const featureChangeStateId = (key: string) => `feature-change-state-${key}`;
export const featureHealthButtonId = (key: string) => `feature-health-${key}`;
export const featureNewStatusId = (key: string) => `feature-new-status-${key}`;
export const featureOpenBoardId = (key: string) => `feature-open-board-${key}`;
// The Change state sheet: one option per state (by its name), the D8 warning and Apply.
export const featureStateSheetId = (key: string) => `feature-state-sheet-${key}`;
export const featureStateOptionId = (stateName: string) => `feature-state-option-${stateName}`;
export const featureStateWarningId = (key: string) => `feature-state-warning-${key}`;
export const featureStateApplyId = (key: string) => `feature-state-apply-${key}`;
// The Health sheet: one option per health, and Not set.
export const featureHealthSheetId = (key: string) => `feature-health-sheet-${key}`;
export const featureHealthOptionId = (health: string) => `feature-health-option-${health}`;

// F3
export const featureBoardStoryId = (key: string) => `feature-board-story-${key}`;
export const featureBoardIds = {
  scroll: 'feature-board-scroll',
  back: 'feature-board-back',
  header: 'feature-board-header',
  key: 'feature-board-key',
  title: 'feature-board-title',
  status: 'feature-board-status',
  statusBy: 'feature-board-status-by',
  statusStale: 'feature-board-status-stale',
  figureStories: 'feature-board-figure-stories',
  figureBugs: 'feature-board-figure-bugs',
  figureTasks: 'feature-board-figure-tasks',
  figurePoints: 'feature-board-figure-points',
  percent: 'feature-board-percent',
  webLink: 'feature-board-web-link',
} as const;
// A section of a Feature with Features under it (depth-first), and one state group inside it. `scope` is the section's
// key, or 'all' on a Feature with no Features under it.
export const featureBoardSectionId = (key: string) => `feature-board-section-${key}`;
export const featureBoardSectionEmptyId = (key: string) => `feature-board-section-empty-${key}`;
export const featureBoardGroupId = (scope: string, category: string) =>
  `feature-board-group-${scope}-${category}`;
export const featureBoardGroupCountId = (scope: string, category: string) =>
  `feature-board-group-count-${scope}-${category}`;
// A story row: its parts, and its task bar with the done, doing and to do segments and the text.
export const featureBoardTypeId = (key: string) => `feature-board-type-${key}`;
export const featureBoardSprintId = (key: string) => `feature-board-sprint-${key}`;
export const featureBoardOwnerId = (key: string) => `feature-board-owner-${key}`;
export const featureBoardTaskBarId = (key: string) => `feature-board-tasks-${key}`;
export const featureBoardTaskDoneId = (key: string) => `feature-board-tasks-done-${key}`;
export const featureBoardTaskDoingId = (key: string) => `feature-board-tasks-doing-${key}`;
export const featureBoardTaskTextId = (key: string) => `feature-board-tasks-text-${key}`;

// F4
export const featureDigestItemId = (key: string) => `feature-digest-item-${key}`;
