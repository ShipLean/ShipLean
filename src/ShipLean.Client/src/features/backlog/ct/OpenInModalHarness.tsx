import { useLocation } from 'react-router-dom';
import { ItemPeekHost } from '../../itemPeek/ItemPeekHost';
import { OpenCommandHost } from '../commands/OpenCommandHost';
import { RowSelectCell } from '../selection/RowSelectCell';
import { SelectionProvider } from '../selection/SelectionProvider';
import { TitleCell } from '../selection/TitleCell';
import { useRowGestures } from '../selection/useRowGestures';
import { useSelection } from '../selection/useSelection';
import { backlogTestIds as ids } from '../testIds';
import type { BacklogRowDto } from '../../../api/views';
import { CommandHarness } from './CommandHarness';
import { itemRow, PROJECT } from './fixtures';

const ROWS = ['SHIP-1', 'SHIP-2', 'SHIP-3'].map((key) => itemRow({ key }));
const IDS = ROWS.map((row) => row.id);

function Row({ row }: { row: BacklogRowDto }) {
  const gestures = useRowGestures(row);
  return (
    <tr {...gestures} data-testid={ids.row} data-key={row.key}>
      <RowSelectCell row={row} />
      <td data-testid={ids.rank}>{row.rank}</td>
      <td>
        <TitleCell row={row} projectKey={PROJECT.key} />
      </td>
    </tr>
  );
}

function Table() {
  useSelection(IDS);
  const { pathname, search } = useLocation();
  return (
    <>
      <OpenCommandHost rows={ROWS} projectKey={PROJECT.key} />
      <output data-testid="harness-address">{pathname + search}</output>
      <table>
        <tbody>
          {ROWS.map((row) => (
            <Row key={row.id} row={row} />
          ))}
        </tbody>
      </table>
      <ItemPeekHost project={PROJECT} />
    </>
  );
}

// CT-only: three backlog rows with the real title cell and row gestures, the `open` command (run it with
// CommandHarness's buttons), the item modal and the address.
export function OpenInModalHarness() {
  return (
    <CommandHarness>
      <SelectionProvider>
        <Table />
      </SelectionProvider>
    </CommandHarness>
  );
}
