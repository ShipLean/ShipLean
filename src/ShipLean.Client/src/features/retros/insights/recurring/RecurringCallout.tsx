import type { RetroInsightsMode } from '@shiplean/core/api/retros';

import { retrosTestIds as ids } from '../../testIds';

/** The week-mode recurrence window, whatever range is shown: `RetroRecurrence.Weeks.Window` (K11). */
const RECURRENCE_WEEKS = 6;

/** The warn callout above the recurring table; hidden when no theme is recurring. */
export function RecurringCallout({ count, mode }: { count: number; mode: RetroInsightsMode }) {
  if (count === 0) return null;
  const noun = count === 1 ? 'theme keeps' : 'themes keep';
  return (
    <div
      role="alert"
      data-testid={ids.retroRecurringCallout}
      data-callout="recurring"
      className="rounded-panel border border-edge border-l-[3px] border-l-caution bg-sunken px-3 py-2 text-[length:var(--text-sm)]"
    >
      <b className="block">
        {count} {noun} coming back
      </b>
      {mode === 'week'
        ? `Counted over the last ${RECURRENCE_WEEKS} weeks, by the day each point was logged.`
        : 'They were mentioned in several recent sprints without a change that helped.'}
    </div>
  );
}
