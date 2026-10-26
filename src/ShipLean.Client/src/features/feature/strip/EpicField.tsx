import type { ParentRefDto } from '../../../api/workItems';
import { TypeIcon } from '../../../components/TypeIcon';
import { ItemLink } from '../../itemPeek/ItemLink';
import { FieldCell } from '../../workItem/form/FieldCell';
import { FieldMenu } from '../../workItem/form/FieldMenu';
import { ParentCandidates } from '../../workItem/form/ParentCandidates';
import { parentTypesOf } from '../../workItem/form/ParentField';
import { featureTestIds as ids } from '../testIds';

// The Feature's parent Epic: the story form's parent picker, offering what a Feature may sit under (Epics, and Features shown as Epic › Feature).
export function EpicField({
  projectId,
  itemId,
  value,
  changed,
  onChange,
}: {
  projectId: string;
  itemId: string;
  value: ParentRefDto | null;
  changed: boolean;
  onChange: (parent: ParentRefDto | null) => void;
}) {
  return (
    <FieldCell label="Epic" field="parent" changed={changed} className="max-w-[420px] shrink">
      <div className="flex items-center gap-2">
        <FieldMenu
          buttonTestId={ids.stripEpic}
          buttonLabel="Epic"
          button={
            value ? (
              <>
                <TypeIcon type={value.type.id} />
                <span className="truncate">{value.title}</span>
              </>
            ) : (
              <span className="text-ink-muted">No epic</span>
            )
          }
        >
          {(close) => (
            <ParentCandidates
              projectId={projectId}
              itemId={itemId}
              types={parentTypesOf('feature')}
              value={value}
              onPick={(parent) => {
                onChange(parent);
                close();
              }}
            />
          )}
        </FieldMenu>
        {value ? (
          <ItemLink
            testId={ids.epicOpen}
            itemKey={value.key}
            title="Open epic"
            className="text-[length:var(--text-xs)] text-signal hover:underline"
          >
            {value.key}
          </ItemLink>
        ) : null}
      </div>
    </FieldCell>
  );
}
