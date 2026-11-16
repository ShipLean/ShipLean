import type { SprintRetroDto } from '@shiplean/core/api/retros';
import { Sheet } from '../../../touch/Sheet';
import { retrosTestIds as ids } from '../testIds';
import { ActionItemsSection } from './rail/ActionItemsSection';
import { CoverageSection } from './rail/CoverageSection';
import { RecurringSection } from './rail/RecurringSection';

export type RailSheet = 'coverage' | 'actions' | 'recurring';

const TITLES: Record<RailSheet, string> = {
  coverage: 'Coverage',
  actions: 'Action items',
  recurring: 'Recurring themes',
};

/**
 * At touch the rail's sections open as bottom sheets (D2): the same sections the rail draws, one at a time, in their
 * sheet variant (the sheet's title names them). Recurring themes ends with Open insights.
 */
export function RetroRailSheets({
  retro,
  open,
  onClose,
  onOpenInsights,
}: {
  retro: SprintRetroDto;
  open: RailSheet | null;
  onClose: () => void;
  onOpenInsights: () => void;
}) {
  return (
    <Sheet
      open={open !== null}
      title={open ? TITLES[open] : ''}
      onClose={onClose}
      footer={
        open === 'recurring' ? (
          <button
            type="button"
            data-testid={ids.retroRecurringOpenInsights}
            className="btn-ghost w-full"
            onClick={onOpenInsights}
          >
            Open insights
          </button>
        ) : undefined
      }
    >
      {open === 'coverage' ? <CoverageSection sheet rows={retro.coverage} /> : null}
      {open === 'actions' ? <ActionItemsSection sheet rows={retro.actionItems} /> : null}
      {open === 'recurring' ? (
        <RecurringSection sheet strips={retro.recurring} sprints={retro.sprints} />
      ) : null}
    </Sheet>
  );
}
