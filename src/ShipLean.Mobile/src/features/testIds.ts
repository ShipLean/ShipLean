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

// F3
export const featureBoardStoryId = (key: string) => `feature-board-story-${key}`;

// F4
export const featureDigestItemId = (key: string) => `feature-digest-item-${key}`;
