import type { WorkItemTypeId } from '../../../api/refs';
import type { ParentRefDto } from '../../../api/workItems';
import { TypeIcon } from '../../../components/TypeIcon';
import { workItemTestIds as ids } from '../testIds';
import { FieldCell } from './FieldCell';
import { FieldMenu } from './FieldMenu';
import { ParentCandidates } from './ParentCandidates';

// Epic <- Feature <- Feature | Story | Bug, and Story | Bug <- Task.
export const parentTypesOf = (type: WorkItemTypeId): WorkItemTypeId[] => {
  switch (type) {
    case 'feature':
      return ['epic', 'feature'];
    case 'story':
    case 'bug':
      return ['feature'];
    case 'task':
      return ['story', 'bug'];
    default:
      return [];
  }
};

export function ParentField({
  projectId,
  itemId,
  type,
  value,
  changed,
  onChange,
}: {
  projectId: string;
  itemId: string;
  type: WorkItemTypeId;
  value: ParentRefDto | null;
  changed: boolean;
  onChange: (parent: ParentRefDto | null) => void;
}) {
  const types = parentTypesOf(type);
  if (types.length === 0) return null;
  return (
    <FieldCell label="Parent" field="parent" changed={changed} className="max-w-[420px] shrink">
      <FieldMenu
        buttonTestId={ids.parentButton}
        buttonLabel="Parent"
        button={
          value ? (
            <>
              <TypeIcon type={value.type.id} />
              <span className="truncate">{value.title}</span>
            </>
          ) : (
            <span className="text-ink-muted">No parent</span>
          )
        }
      >
        {(close) => (
          <ParentCandidates
            projectId={projectId}
            itemId={itemId}
            types={types}
            value={value}
            onPick={(parent) => {
              onChange(parent);
              close();
            }}
          />
        )}
      </FieldMenu>
    </FieldCell>
  );
}
