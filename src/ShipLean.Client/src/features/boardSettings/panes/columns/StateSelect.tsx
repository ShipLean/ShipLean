import type { StateRef, WorkItemTypeId } from '../../../../api/refs';
import { boardSettingsTestIds as ids } from '../../testIds';
import { typeNames } from './columnEdits';

const testIdOf: Record<WorkItemTypeId, string | undefined> = {
  epic: ids.columnStateEpic,
  feature: ids.columnStateFeature,
  story: ids.columnStateStory,
  bug: ids.columnStateBug,
  task: undefined,
};

/** The state one column means for one work item type. */
export function StateSelect({
  type,
  columnName,
  states,
  value,
  disabled,
  onChange,
}: {
  type: WorkItemTypeId;
  columnName: string;
  states: StateRef[];
  value: string | undefined;
  disabled: boolean;
  onChange(stateId: string): void;
}) {
  const live = states.some((state) => state.id === value);
  return (
    <select
      data-testid={testIdOf[type]}
      className={`field h-[26px] w-full min-w-0 px-1 text-[length:var(--text-sm)] ${live ? '' : 'border-caution text-caution'}`}
      aria-invalid={!live}
      aria-label={`${typeNames[type]} state for column ${columnName}`}
      value={live ? value : ''}
      disabled={disabled}
      onChange={(event) => onChange(event.target.value)}
    >
      {live ? null : (
        <option value="" disabled>
          Removed state — pick one
        </option>
      )}
      {states.map((state) => (
        <option key={state.id} value={state.id}>
          {state.name}
        </option>
      ))}
    </select>
  );
}
