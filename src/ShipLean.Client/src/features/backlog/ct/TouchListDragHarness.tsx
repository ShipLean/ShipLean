import { useState } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { SelectionProvider } from '../selection/SelectionProvider';
import { TouchSelectModeProvider } from '../touch/selectMode';
import { TouchRowList } from '../touch/TouchRowList';

// CT-only: the touch row list with the real drag handles, and a button that removes the first row, as a teammate's
// change would while a finger holds it. The button sits last, so pressing it never moves a row.
export const REMOVE_FIRST_ROW_ID = 'harness-remove-first-row';

export function TouchListDragHarness({ rows }: { rows: BacklogRowDto[] }) {
  const [shown, setShown] = useState(rows);
  return (
    <TouchSelectModeProvider>
      <SelectionProvider>
        <TouchRowList
          rows={shown}
          projectKey="SHIP"
          handles
          onHandleDrop={() => undefined}
          flashing={new Set()}
          filtered={false}
        />
        <button
          type="button"
          data-testid={REMOVE_FIRST_ROW_ID}
          onClick={() => setShown((now) => now.slice(1))}
        >
          Remove first
        </button>
      </SelectionProvider>
    </TouchSelectModeProvider>
  );
}
