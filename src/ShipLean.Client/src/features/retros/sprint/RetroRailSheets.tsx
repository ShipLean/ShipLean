import type { SprintRetroDto } from '@shiplean/core/api/retros';
import { Sheet } from '../../../touch/Sheet';
import { ActionItemsSection } from './rail/ActionItemsSection';
import { CoverageSection } from './rail/CoverageSection';
import { RecurringSection } from './rail/RecurringSection';

export type RailSheet = 'coverage' | 'actions' | 'recurring';

const TITLES: Record<RailSheet, string> = {
  coverage: 'Coverage',
  actions: 'Action items',
  recurring: 'Recurring themes',
};

/** At touch the rail's sections open as bottom sheets (D2): the same sections the rail draws, one at a time. */
export function RetroRailSheets({
  retro,
  open,
  onClose,
}: {
  retro: SprintRetroDto;
  open: RailSheet | null;
  onClose: () => void;
}) {
  return (
    <Sheet open={open !== null} title={open ? TITLES[open] : ''} onClose={onClose}>
      {open === 'coverage' ? <CoverageSection rows={retro.coverage} /> : null}
      {open === 'actions' ? <ActionItemsSection rows={retro.actionItems} /> : null}
      {open === 'recurring' ? (
        <RecurringSection strips={retro.recurring} sprints={retro.sprints} />
      ) : null}
    </Sheet>
  );
}
