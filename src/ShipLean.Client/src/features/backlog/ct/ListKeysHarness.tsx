import { useState } from 'react';
import { Dialog } from '../../../components/Dialog';
import { QuickSearchProvider } from '../../quickSearch/host/QuickSearchProvider';
import { KeysButton } from '../commands/KeysButton';
import { KeysDialog } from '../commands/KeysDialog';
import { ListKeysHost } from '../commands/ListKeysHost';
import { ListCommandsProvider, useListCommand } from '../commands/listCommands';
import { RowSelectCell } from '../selection/RowSelectCell';
import { SelectAllCell } from '../selection/SelectAllCell';
import { SelectionProvider } from '../selection/SelectionProvider';
import { useRowGestures } from '../selection/useRowGestures';
import { useSelection } from '../selection/useSelection';
import { backlogTestIds as ids } from '../testIds';
import type { BacklogRowDto } from '../../../api/views';
import { itemRow } from './fixtures';

const KEYS = ['SHIP-1', 'SHIP-2', 'SHIP-3', 'SHIP-4'];
const make = () => KEYS.map((key) => itemRow({ key }));
// SHIP-3 sits in a collapsed section: shown nowhere, so the keys skip it.
const COLLAPSED = 'id-SHIP-3';

function Row({ row }: { row: BacklogRowDto }) {
  const gestures = useRowGestures(row);
  return (
    <tr
      {...gestures}
      data-testid={ids.row}
      data-key={row.key}
      style={row.id === COLLAPSED ? { display: 'none' } : undefined}
    >
      <RowSelectCell row={row} />
      <td>{row.title}</td>
    </tr>
  );
}

// Stands in for the commands other tasks register: it logs each run as `name` or `name:arg`.
function Fakes({ log }: { log: (line: string) => void }) {
  useListCommand('open', (arg) => log(`open${arg === undefined ? '' : `:${String(arg)}`}`));
  useListCommand('estimate', (arg) => log(`estimate:${String(arg)}`));
  useListCommand('moveMenu', (arg) => log(`moveMenu:${String(arg)}`));
  useListCommand('addBelow', () => log('addBelow'));
  useListCommand('search', () => log('search'));
  return null;
}

function Screen({
  rows,
  setRows,
}: {
  rows: BacklogRowDto[];
  setRows: (update: (current: BacklogRowDto[]) => BacklogRowDto[]) => void;
}) {
  const [lines, setLines] = useState<string[]>([]);
  const [modal, setModal] = useState(false);
  const selection = useSelection(rows.map((row) => row.id));
  return (
    <>
      <Fakes log={(line) => setLines((current) => [...current, line])} />
      <ListKeysHost />
      <KeysButton />
      <KeysDialog />
      <output data-testid="key-log">{lines.join(',')}</output>
      <output data-testid="focused-id">{selection.focusedId ?? 'none'}</output>
      <output data-testid="selected-count">{selection.count}</output>
      <button type="button" data-testid="refetch" onClick={() => setRows(() => make())}>
        refetch
      </button>
      <button
        type="button"
        data-testid="remove-2"
        onClick={() => setRows((current) => current.filter((row) => row.key !== 'SHIP-2'))}
      >
        remove
      </button>
      <button type="button" data-testid="open-modal" onClick={() => setModal(true)}>
        modal
      </button>
      <button type="button" data-testid="outside">
        outside
      </button>
      <input data-testid="field" aria-label="A field" />
      {modal ? (
        <Dialog title="An item" onClose={() => setModal(false)}>
          <p>Body</p>
        </Dialog>
      ) : null}
      <table>
        <thead>
          <tr>
            <SelectAllCell />
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <Row key={row.id} row={row} />
          ))}
        </tbody>
      </table>
    </>
  );
}

// CT-only: four item rows (the third in a collapsed section) with the real row gestures, selection, list keys and key
// dialog. The open, estimate, moveMenu, addBelow and search commands are fakes that write to `key-log`.
export function ListKeysHarness() {
  const [rows, setRows] = useState(make);
  const walkOrder = rows.map((row) => row.id).filter((id) => id !== COLLAPSED);
  return (
    <ListCommandsProvider>
      <QuickSearchProvider>
        <SelectionProvider walkOrder={walkOrder}>
          <Screen rows={rows} setRows={setRows} />
        </SelectionProvider>
      </QuickSearchProvider>
    </ListCommandsProvider>
  );
}
