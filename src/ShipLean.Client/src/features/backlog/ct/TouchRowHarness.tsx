import { useEffect, useState } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { SelectionProvider } from '../selection/SelectionProvider';
import { TouchSelectModeProvider, useTouchSelectMode } from '../touch/selectMode';
import { TouchRowList } from '../touch/TouchRowList';

// A stand-in for wave 2's drag handle, so a test can see whether the list put renderHandle's output in the slot.
const HARNESS_HANDLE_ID = 'harness-touch-handle';
// The line the real handle's drop leaves, for a test to read what onHandleDrop was given.
const HARNESS_DROP_LOG_ID = 'harness-touch-drop-log';

function Rows({
  rows,
  handles,
  withHandle,
  selecting,
  dropLog,
}: {
  rows: BacklogRowDto[];
  handles: boolean;
  withHandle: boolean;
  selecting: boolean;
  dropLog: boolean;
}) {
  const [drops, setDrops] = useState<string[]>([]);
  const mode = useTouchSelectMode();
  const { start } = mode;
  useEffect(() => {
    if (selecting) start();
  }, [selecting, start]);
  return (
    <>
      <TouchRowList
        rows={rows}
        projectKey="SHIP"
        handles={handles}
        renderHandle={
          withHandle
            ? (row) => (
                <span data-testid={HARNESS_HANDLE_ID} data-row-id={row.id}>
                  ⠿
                </span>
              )
            : undefined
        }
        flashing={new Set()}
        onHandleDrop={
          dropLog
            ? (rowId, targetId) => setDrops((previous) => [...previous, `${rowId} on ${targetId}`])
            : undefined
        }
        filtered={!handles}
      />
      <ol data-testid={HARNESS_DROP_LOG_ID}>
        {drops.map((line) => (
          <li key={line}>{line}</li>
        ))}
      </ol>
    </>
  );
}

// CT-only: touch rows under the selection and the touch Select mode, as the Stories List mounts them at touch.
export function TouchRowHarness({
  rows,
  handles = true,
  withHandle = false,
  selecting = false,
  dropLog = false,
}: {
  rows: BacklogRowDto[];
  handles?: boolean;
  withHandle?: boolean;
  selecting?: boolean;
  // Gives the list onHandleDrop (the real TouchHandle) and shows what it was called with.
  dropLog?: boolean;
}) {
  return (
    <TouchSelectModeProvider>
      <SelectionProvider>
        <Rows
          rows={rows}
          handles={handles}
          withHandle={withHandle}
          selecting={selecting}
          dropLog={dropLog}
        />
      </SelectionProvider>
    </TouchSelectModeProvider>
  );
}
