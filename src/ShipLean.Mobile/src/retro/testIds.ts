// The Retro area's own test ids. Each block belongs to the task named in its comment.
export const retroIds = {
  // RT1
  segment: 'retro-segment',
  title: 'retro-title',
  addPoint: 'retro-add-point',
  tiles: 'retro-tiles',
  list: 'retro-list',
  // RT2
  addSheet: 'sheet-retro-add',
  addText: 'retro-add-text',
  addSubmit: 'retro-add-submit',
  // RT3a
  toDecide: 'retro-to-decide',
  toDecideEmpty: 'retro-to-decide-empty',
  toDecideInsightsError: 'retro-to-decide-insights-error',
  toDecideInsightsRetry: 'retro-to-decide-insights-retry',
  // RT3b
  decideSheet: 'sheet-retro-decide',
  // RT4
  features: 'retro-features',
} as const;

// RT1: ids that carry a category, a theme or a point.
export const retroTileId = (category: string) => `retro-tile-${category}`;
export const retroTileCountId = (category: string) => `retro-tile-${category}-count`;
export const retroTileToDecideId = (category: string) => `retro-tile-${category}-to-decide`;
// A theme's heading; the unthemed group is `none`.
export const retroThemeId = (themeId: string | null) => `retro-theme-${themeId ?? 'none'}`;
export const retroRecurringId = (themeId: string | null) =>
  `retro-theme-${themeId ?? 'none'}-recurring`;
export const retroPointId = (pointId: string) => `retro-point-${pointId}`;
export const retroPointAuthorId = (pointId: string) => `retro-point-${pointId}-author`;
export const retroPointTextId = (pointId: string) => `retro-point-${pointId}-text`;
export const retroPlusId = (pointId: string) => `retro-point-${pointId}-plus`;
export const retroFacesId = (pointId: string) => `retro-point-${pointId}-faces`;
export const retroFaceId = (pointId: string, userId: string) =>
  `retro-point-${pointId}-face-${userId}`;
export const retroMoreFacesId = (pointId: string) => `retro-point-${pointId}-more-faces`;
export const retroAddActionId = (pointId: string) => `retro-point-${pointId}-add-action`;
export const retroNotNeededId = (pointId: string) => `retro-point-${pointId}-not-needed`;
export const retroActionKeyId = (pointId: string) => `retro-point-${pointId}-action-key`;
export const retroNotNeededReasonId = (pointId: string) => `retro-point-${pointId}-reason`;

// RT2: a category chip of the Add a point sheet.
export const retroAddCategoryId = (category: string) => `retro-add-category-${category}`;

// RT3a: the To decide view. A card is the retro card's id; a proposal is the merge proposal's id. The point rows reuse
// retroPointId, retroPointTextId, retroAddActionId and retroNotNeededId.
export const retroDecideCardId = (cardId: string) => `retro-decide-card-${cardId}`;
export const retroDecideLogToggleId = (cardId: string) => `retro-decide-card-${cardId}-log-toggle`;
export const retroDecideLogId = (cardId: string) => `retro-decide-card-${cardId}-log`;
export const retroDecideCauseId = (pointId: string) => `retro-point-${pointId}-cause`;
export const retroProposalId = (proposalId: string) => `retro-proposal-${proposalId}`;
export const retroProposalMergeId = (proposalId: string) => `retro-proposal-${proposalId}-merge`;
export const retroProposalKeepId = (proposalId: string) => `retro-proposal-${proposalId}-keep`;
