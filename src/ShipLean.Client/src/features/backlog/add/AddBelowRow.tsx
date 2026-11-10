import { useRef } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { useSelection } from '../selection/useSelection';
import { AddBelowCommand } from './AddBelowCommand';
import { AddRowFields } from './AddRowFields';
import { useAddChain, useBelowControls, useBelowState } from './useAddChain';
import { backlogTestIds as ids } from '../testIds';

export interface AddBelowRowProps {
  /** The row this sits under. */
  row: BacklogRowDto;
  columnCount: number;
  projectId: string;
  projectKey: string;
  /** A filter, search or check queue is on: rank is relative to rows you can't see. */
  filtering: boolean;
  /** The list is sorted by a column, so the row under this one is not the next in rank. */
  sorted?: boolean;
}

// The inline add row under a row. One is mounted under every row; the `addBelow` command opens the one under the
// focused row. Each Enter makes a row just below the one before, so the typed list keeps its order: the open row
// then moves under the row it made.
export function AddBelowRow({
  row,
  columnCount,
  projectId,
  filtering,
  sorted = false,
}: AddBelowRowProps) {
  const selection = useSelection();
  const state = useBelowState();
  const chain = useAddChain();
  const controls = useBelowControls();
  const cell = useRef<HTMLTableCellElement>(null);

  const command =
    selection.focusedId === row.id ? (
      <AddBelowCommand rowId={row.id} filtering={filtering} sorted={sorted} />
    ) : null;
  if (state === null || state.anchorId !== row.id) return command;

  function close() {
    // Focus goes back to the row above before this row leaves.
    (cell.current?.parentElement?.previousElementSibling as HTMLElement | null)?.focus();
    controls.close();
  }

  return (
    <>
      {command}
      <tr data-testid={ids.addRow} data-anchor={row.key} className="h-11 bg-signal-soft">
        <td ref={cell} colSpan={columnCount} className="px-2">
          <AddRowFields
            projectId={projectId}
            type={state.type}
            parent={state.parent}
            draft={state.draft}
            onDraft={controls.draft}
            onType={(type) => controls.stick({ type })}
            onParent={(parent) => controls.stick({ parent })}
            hint={`Goes below ${row.key}`}
            error={chain.error}
            onClose={close}
            onAdd={chain.below}
          />
        </td>
      </tr>
    </>
  );
}
