import { retrosTestIds as ids } from '../../testIds';
import type { SprintRetroSlotProps } from '../SprintRetroSlotProps';
import { ActionItemsSection } from './ActionItemsSection';
import { CoverageSection } from './CoverageSection';
import { RecurringSection } from './RecurringSection';

/** The retro rail: coverage, action items and recurring themes. */
export function RetroRail({ retro }: SprintRetroSlotProps) {
  return (
    <aside data-testid={ids.retroRail} className="grid content-start gap-4">
      <CoverageSection rows={retro.coverage} />
      <ActionItemsSection rows={retro.actionItems} />
      <RecurringSection strips={retro.recurring} sprints={retro.sprints} />
    </aside>
  );
}
