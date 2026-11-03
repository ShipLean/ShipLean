import { ESTIMATE_ROWS } from './estimateRows';
import { CommandHarness } from './CommandHarness';
import { PointsCell } from '../estimate/PointsCell';
import { SelectionProvider } from '../selection/SelectionProvider';
import { useRowGestures } from '../selection/useRowGestures';
import { useSelection } from '../selection/useSelection';
import type { BacklogRowDto } from '../../../api/views';
import { backlogTestIds as ids } from '../testIds';

const IDS = ESTIMATE_ROWS.map((row) => row.id);

function Row({ row }: { row: BacklogRowDto }) {
  const gestures = useRowGestures(row);
  return (
    <tr {...gestures} data-testid={ids.row} data-key={row.key}>
      <td className="pl-[420px]">
        <PointsCell row={row} />
      </td>
    </tr>
  );
}

function Table() {
  useSelection(IDS);
  return (
    <table>
      <tbody>
        {ESTIMATE_ROWS.map((row) => (
          <Row key={row.id} row={row} />
        ))}
      </tbody>
    </table>
  );
}

// CT-only: three rows with points cells under the list commands and a selection (or, as Tree mode, without one).
export function EstimateHarness({ tree = false }: { tree?: boolean }) {
  return (
    <CommandHarness>
      {tree ? (
        <Table />
      ) : (
        <SelectionProvider>
          <Table />
        </SelectionProvider>
      )}
    </CommandHarness>
  );
}
