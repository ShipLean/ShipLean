import type { ParentRefDto } from '@shiplean/core/api/workItems';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { Sheet } from '../../../touch/Sheet';
import { parentTypesOf } from '../../workItem/form/ParentField';
import { ParentCandidates } from '../../workItem/form/ParentCandidates';
import { backlogTestIds as ids } from '../testIds';

/** The row's Parent › sheet: the parent search (not focused until tapped) over the parents this type may have. */
export function RowParentSheet({
  row,
  projectId,
  onPick,
  onClose,
}: {
  row: BacklogRowDto;
  projectId: string;
  onPick: (parent: ParentRefDto | null) => void;
  onClose: () => void;
}) {
  return (
    <Sheet open title="Parent" onClose={onClose}>
      <div data-testid={ids.rowParentSheet} className="grid gap-1">
        {row.type === null ? null : (
          <ParentCandidates
            projectId={projectId}
            itemId={row.id}
            types={parentTypesOf(row.type.id)}
            value={row.parent}
            onPick={(parent) => {
              onPick(parent);
              onClose();
            }}
          />
        )}
      </div>
    </Sheet>
  );
}
