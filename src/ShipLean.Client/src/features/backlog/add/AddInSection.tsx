import { useState } from 'react';
import type { WorkItemTypeId } from '../../../api/refs';
import type { BacklogSectionDto } from '../../../api/views';
import type { ParentRefDto } from '../../../api/workItems';
import { backlogTestIds as ids } from '../testIds';
import { AddRowFields } from './AddRowFields';
import { useAddChain } from './useAddChain';

export interface AddInSectionProps {
  section: BacklogSectionDto;
  columnCount: number;
  projectId: string;
  projectKey: string;
  /** A filter, search or check queue is on: rank is relative to rows you can't see. */
  filtering: boolean;
}

// The "+ Add" row at the end of a Planning section. It opens the add row, whose items go last in this section's
// sprint; the type and parent stay until it closes, so a typed list keeps its order.
export function AddInSection({
  section,
  columnCount,
  projectId,
  projectKey,
  filtering,
}: AddInSectionProps) {
  const [open, setOpen] = useState(false);
  const [type, setType] = useState<WorkItemTypeId>('story');
  const [parent, setParent] = useState<ParentRefDto | null>(null);
  const chain = useAddChain(projectId, projectKey);
  const sprintId = section.sprint?.id ?? null;

  if (!open) {
    return (
      <tr>
        <td colSpan={columnCount} className="px-2 py-1">
          <button
            type="button"
            data-testid={ids.addHere}
            className="btn-ghost"
            disabled={filtering}
            title={
              filtering
                ? "Off while a filter is on: rank is relative to rows you can't see"
                : undefined
            }
            onClick={() => setOpen(true)}
          >
            + Add
          </button>
        </td>
      </tr>
    );
  }
  return (
    <tr data-testid={ids.addRow} data-section={section.id} className="h-11 bg-signal-soft">
      <td colSpan={columnCount} className="px-2">
        <AddRowFields
          projectId={projectId}
          type={type}
          parent={parent}
          onType={setType}
          onParent={setParent}
          hint={`Goes at the end of ${section.title}`}
          error={chain.error}
          onClose={() => {
            setOpen(false);
            setParent(null);
            setType('story');
            chain.clearError();
          }}
          onAdd={async (fields) => (await chain.inSprint(sprintId, fields)) !== null}
        />
      </td>
    </tr>
  );
}
