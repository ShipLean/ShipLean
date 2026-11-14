import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import type { ParentRefDto } from '@shiplean/core/api/workItems';
import { Sheet } from '../../../touch/Sheet';
import { backlogTestIds as ids } from '../testIds';
import { ParentMenuBody } from './ParentMenuBody';

/** The bar's Parent sheet: the parent search over the parents every selected type may have (or a line saying none is). */
export function BulkParentSheet({
  projectId,
  types,
  onPick,
  onClose,
}: {
  projectId: string;
  types: WorkItemTypeId[];
  onPick: (parent: ParentRefDto | null) => void;
  onClose: () => void;
}) {
  return (
    <Sheet open title="Parent" onClose={onClose}>
      <div data-testid={ids.bulkSheetParent} className="grid gap-1">
        <ParentMenuBody
          projectId={projectId}
          types={types}
          onPick={(parent) => {
            onPick(parent);
            onClose();
          }}
        />
      </div>
    </Sheet>
  );
}
