import type { StateRef, WorkItemTypeId } from '../../../api/refs';
import { StateDot } from '../../../components/StateDot';
import { StateMenu } from '../../../components/StateMenu';
import { FieldCell } from './FieldCell';
import { FieldMenu } from './FieldMenu';
import { useProjectStates } from '../workItemApi';
import { workItemTestIds as ids } from '../testIds';

export function StateField({
  projectId,
  type,
  value,
  changed,
  onChange,
}: {
  projectId: string;
  type: WorkItemTypeId;
  value: StateRef;
  changed: boolean;
  onChange: (state: StateRef) => void;
}) {
  const states = useProjectStates(projectId);
  const ofType = (states.data ?? [])
    .filter((state) => state.type === type)
    .sort((a, b) => a.order - b.order)
    .map(({ id, name, category }) => ({ id, name, category }));
  return (
    <FieldCell label="State" field="state" changed={changed}>
      <FieldMenu
        buttonTestId={ids.stateButton}
        buttonLabel="State"
        button={<StateDot category={value.category} name={value.name} />}
      >
        {(close) => (
          <StateMenu
            states={ofType}
            currentId={value.id}
            onClose={close}
            onSelect={(id) => {
              const picked = ofType.find((state) => state.id === id);
              if (picked) onChange(picked);
              close();
            }}
          />
        )}
      </FieldMenu>
    </FieldCell>
  );
}
