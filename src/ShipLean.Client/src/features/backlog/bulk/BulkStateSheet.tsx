import type { StateDto } from '@shiplean/core/api/projects';
import { StateDot } from '../../../components/StateDot';
import { actionSheetRowClass } from '../../../touch/ActionSheet';
import { Sheet } from '../../../touch/Sheet';
import { backlogTestIds as ids } from '../testIds';

/** The bar's State sheet: every state name a selected row's type has; each is applied to a row's own type. */
export function BulkStateSheet({
  names,
  states,
  onPick,
  onClose,
}: {
  names: readonly string[];
  states: readonly StateDto[];
  onPick: (name: string) => void;
  onClose: () => void;
}) {
  return (
    <Sheet open title="State" onClose={onClose}>
      {names.map((name) => (
        <button
          key={name}
          type="button"
          data-testid={ids.bulkSheetOption}
          className={`${actionSheetRowClass} text-ink`}
          onClick={() => {
            onPick(name);
            onClose();
          }}
        >
          <StateDot
            category={states.find((state) => state.name === name)?.category ?? 'proposed'}
            name={name}
          />
        </button>
      ))}
    </Sheet>
  );
}
