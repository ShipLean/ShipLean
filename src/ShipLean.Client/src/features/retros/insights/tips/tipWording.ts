import type { RetroInsightsMode, RetroThemeStatus, RetroVerdict } from '@shiplean/core/api/retros';

const unit = (mode: RetroInsightsMode) => (mode === 'week' ? 'week' : 'sprint');

/** What each status means; Recurring's window follows the mode (the rule is 3 of the last 4 sprints or 6 weeks). */
export function statusWording(status: RetroThemeStatus, mode: RetroInsightsMode): string {
  switch (status) {
    case 'recurring':
      return mode === 'week'
        ? 'Mentioned in 3 of the last 6 weeks, and no closed action item on the theme has helped.'
        : 'Mentioned in 3 of the last 4 sprints, and no closed action item on the theme has helped.';
    case 'beingAddressed':
      return 'Not recurring, and an action item on this theme is still open.';
    case 'resolved':
      return 'Not recurring, and a closed action item on this theme has the verdict Helped.';
    case 'quiet':
      return 'Not recurring, with no open action item and no fix that helped.';
  }
}

export function verdictWording(verdict: RetroVerdict, mode: RetroInsightsMode): string {
  const u = unit(mode);
  switch (verdict) {
    case 'helped':
      return `Mentions per ${u} after the fix are at most half of what they were before.`;
    case 'someEffect':
      return `Mentions per ${u} after the fix are lower than before, but more than half.`;
    case 'noEffect':
      return `Mentions per ${u} after the fix are the same or higher than before.`;
    case 'tooEarly':
      return `No full ${u} has ended since the fix closed.`;
  }
}

export function statusHeadIntro(mode: RetroInsightsMode): string {
  return `Worked out from every ${unit(mode)}, not only the ones shown. No sprint points never count.`;
}

export const verdictHeadIntro = 'What happened to a theme after its last action item closed.';

/** The order the What these mean sheet lists the statuses in; labels and tones are `STATUS` in StatusChip. */
export const STATUS_ORDER: readonly RetroThemeStatus[] = [
  'recurring',
  'beingAddressed',
  'resolved',
  'quiet',
];

/** The order the What these mean sheet lists the verdicts in; labels and tones are `VERDICT` in VerdictChip. */
export const VERDICT_ORDER: readonly RetroVerdict[] = [
  'helped',
  'someEffect',
  'noEffect',
  'tooEarly',
];

/**
 * The entries of the What these mean sheet, as `sheet=defs:<entry>` names them: a status or verdict id, `nosprint`,
 * `unthemed`, or a group (`status`, `verdicts`) for a column heading, which scrolls to its group without highlighting a row.
 */
export type DefsEntry =
  RetroThemeStatus | RetroVerdict | 'nosprint' | 'unthemed' | 'status' | 'verdicts';

export const DEFS_ENTRIES: readonly DefsEntry[] = [
  ...STATUS_ORDER,
  ...VERDICT_ORDER,
  'nosprint',
  'unthemed',
  'status',
  'verdicts',
];

export const isDefsEntry = (value: string): value is DefsEntry =>
  (DEFS_ENTRIES as readonly string[]).includes(value);

export const noSprintWording =
  'Points on retros with no sprint. They count in Total, but not towards Recurring or a verdict, because they have no time order. Week mode places them by the day they were logged.';

export const unthemedWording =
  'Points with no theme, in every category except Went well. Give them one in Search, or run Decide what to do, which assigns themes as it goes.';
