import { useEffect } from 'react';
import type { BacklogRowDto } from '../../../api/views';
import { SelectionProvider } from '../selection/SelectionProvider';
import { TouchSelectModeProvider, useTouchSelectMode } from '../touch/selectMode';
import { TouchRowList } from '../touch/TouchRowList';

// A stand-in for wave 2's drag handle, so a test can see whether the list put renderHandle's output in the slot.
const HARNESS_HANDLE_ID = 'harness-touch-handle';

function Rows({
  rows,
  handles,
  withHandle,
  selecting,
}: {
  rows: BacklogRowDto[];
  handles: boolean;
  withHandle: boolean;
  selecting: boolean;
}) {
  const mode = useTouchSelectMode();
  const { start } = mode;
  useEffect(() => {
    if (selecting) start();
  }, [selecting, start]);
  return (
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
      filtered={!handles}
    />
  );
}

// CT-only: touch rows under the selection and the touch Select mode, as the Stories List mounts them at touch.
export function TouchRowHarness({
  rows,
  handles = true,
  withHandle = false,
  selecting = false,
}: {
  rows: BacklogRowDto[];
  handles?: boolean;
  withHandle?: boolean;
  selecting?: boolean;
}) {
  return (
    <TouchSelectModeProvider>
      <SelectionProvider>
        <Rows rows={rows} handles={handles} withHandle={withHandle} selecting={selecting} />
      </SelectionProvider>
    </TouchSelectModeProvider>
  );
}
