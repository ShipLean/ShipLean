import { itemRow } from './fixtures';
import { CommandHarness } from './CommandHarness';
import { RowSelectCell } from '../selection/RowSelectCell';
import { SelectAllCell } from '../selection/SelectAllCell';
import { SelectionProvider } from '../selection/SelectionProvider';
import { useSelection } from '../selection/useSelection';

const ROWS = ['SHIP-1', 'SHIP-2', 'SHIP-3'].map((key) => itemRow({ key }));
const IDS = ROWS.map((row) => row.id);

function Table() {
  const selection = useSelection(IDS);
  return (
    <>
      <span data-testid="selected-count">{selection.count}</span>
      <span data-testid="focused-id">{selection.focusedId ?? 'none'}</span>
      <table>
        <thead>
          <tr>
            <SelectAllCell />
          </tr>
        </thead>
        <tbody>
          {ROWS.map((row) => (
            <tr key={row.id} data-testid="harness-row">
              <RowSelectCell row={row} />
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}

// CT-only: the selection commands (toggleSelect, clear) run by name over three rows, with CommandHarness's buttons.
export function SelectionCommandHarness() {
  return (
    <CommandHarness>
      <SelectionProvider>
        <Table />
      </SelectionProvider>
    </CommandHarness>
  );
}
