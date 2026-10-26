import type { ImportMappingDto, StatusMappingDto } from '../../../api/import';
import type { StateDto } from '../../../api/projects';
import { STATE_CATEGORIES } from '../../../api/refs';
import type { StateCategory } from '../../../api/refs';
import { StateDot } from '../../../components/StateDot';
import { importTestIds as ids } from '../testIds';
import { NewChip } from './NewChip';
import { NEW_STATE, categoryLabels, setStatusCategory, setStatusState } from './mappingEdits';

const pickable = STATE_CATEGORIES.filter((c) => c !== 'removed');

// One Jira status of one type: the state it lands in, or a new state the run creates and gives a board column.
export function StatusRow({
  row,
  ofType,
  mapping,
  onChange,
}: {
  row: StatusMappingDto;
  ofType: StateDto[];
  mapping: ImportMappingDto;
  onChange: (mapping: ImportMappingDto) => void;
}) {
  return (
    <tr data-testid={ids.mappingStatusRow} data-new={row.isNewSinceLastRun}>
      <td>
        {row.jiraStatus} {row.isNewSinceLastRun ? <NewChip /> : null}
      </td>
      <td>{row.count.toLocaleString('en-GB')}</td>
      <td>
        <select
          data-testid={ids.mappingStatusSelect}
          className="field"
          aria-label={`ShipLean state for ${row.jiraStatus}`}
          value={row.createsState ? NEW_STATE : row.targetState}
          onChange={(event) => onChange(setStatusState(mapping, row, event.target.value, ofType))}
        >
          {ofType.map((s) => (
            <option key={s.id} value={s.name}>
              {s.name}
            </option>
          ))}
          <option value={NEW_STATE}>+ Create new state</option>
        </select>
      </td>
      <td>
        {row.createsState ? (
          <span className="flex flex-col gap-1">
            <select
              data-testid={ids.mappingStatusCategory}
              className="field"
              aria-label={`Category for the new state ${row.jiraStatus}`}
              value={row.targetCategory}
              onChange={(event) =>
                onChange(setStatusCategory(mapping, row, event.target.value as StateCategory))
              }
            >
              {pickable.map((c) => (
                <option key={c} value={c}>
                  {categoryLabels[c]}
                </option>
              ))}
            </select>
            <span data-testid={ids.mappingStatusCreates} className="text-ink-muted">
              Will be created and given a board column.
            </span>
          </span>
        ) : (
          <StateDot category={row.targetCategory} name={categoryLabels[row.targetCategory]} />
        )}
      </td>
    </tr>
  );
}
