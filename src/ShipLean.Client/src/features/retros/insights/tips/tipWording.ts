import type { RetroInsightsMode, RetroThemeStatus, RetroVerdict } from '../../../../api/retros';

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
