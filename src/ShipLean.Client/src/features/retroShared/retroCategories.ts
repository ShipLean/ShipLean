import type { RetroCategory } from '@shiplean/core/api/retros';

export const retroCategoryLabels: Record<RetroCategory, string> = {
  wentWell: 'Went well',
  wentBadly: 'Went badly',
  improve: 'Can be improved',
  notInPlan: 'Not in the plan',
};

/** The design's category colours (`--cat-*` in design/screens/retro.css), as token references. */
export const retroCategoryColours: Record<RetroCategory, string> = {
  wentWell: 'var(--color-ok)',
  wentBadly: 'var(--color-danger)',
  improve: 'var(--color-accent)',
  notInPlan: 'var(--color-purple)',
};
