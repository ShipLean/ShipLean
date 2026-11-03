import type { StateDto } from '../../../api/projects';
import { StateDot } from '../../../components/StateDot';
import { backlogTestIds as ids } from '../testIds';
import { menuOptionClass } from './BulkMenus';

/** The state names any selected row's type has; each is applied to a row's own type. */
export function StateMenuBody({
  names,
  states,
  onPick,
}: {
  names: readonly string[];
  states: readonly StateDto[];
  onPick: (name: string) => void;
}) {
  return (
    <>
      {names.map((name) => (
        <button
          key={name}
          type="button"
          role="menuitem"
          data-testid={ids.bulkMenuOption}
          className={menuOptionClass}
          onClick={() => onPick(name)}
        >
          <StateDot
            category={states.find((state) => state.name === name)?.category ?? 'proposed'}
            name={name}
          />
        </button>
      ))}
    </>
  );
}
