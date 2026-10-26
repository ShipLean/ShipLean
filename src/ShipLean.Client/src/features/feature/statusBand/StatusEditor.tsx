import type { StateRef } from '../../../api/refs';
import { StateDot } from '../../../components/StateDot';
import { StateMenu } from '../../../components/StateMenu';
import { FieldMenu } from '../../workItem/form/FieldMenu';
import { textareaClass } from '../../workItem/form/inputClasses';
import { useProjectStates } from '../../workItem/workItemApi';
import type { FeatureRegionProps } from '../FeatureRegionProps';
import { featureTestIds as ids } from '../testIds';

// The band editor: the state menu and the status line, both edits to the form's draft that its Save writes.
export function StatusEditor({
  projectId,
  draft,
  setField,
  onDone,
  onCancel,
}: Pick<FeatureRegionProps, 'draft' | 'setField'> & {
  projectId: string;
  onDone: () => void;
  onCancel: () => void;
}) {
  const states = useProjectStates(projectId);
  const ofType = (states.data ?? [])
    .filter((state) => state.type === 'feature')
    .sort((a, b) => a.order - b.order)
    .map(({ id, name, category }) => ({ id, name, category }));
  const pick = (stateId: string, close: () => void) => {
    const picked: StateRef | undefined = ofType.find((state) => state.id === stateId);
    if (picked) setField({ state: picked });
    close();
  };
  return (
    <div data-testid={ids.statusEditor} className="grid gap-3">
      <div className="grid gap-1">
        <span className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
          State
        </span>
        <FieldMenu
          buttonTestId={ids.statusEditState}
          buttonLabel="State"
          button={<StateDot category={draft.state.category} name={draft.state.name} />}
        >
          {(close) => (
            <StateMenu
              states={ofType}
              currentId={draft.state.id}
              onClose={close}
              onSelect={(stateId) => pick(stateId, close)}
            />
          )}
        </FieldMenu>
      </div>
      <label className="grid gap-1">
        <span className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
          Status · one honest line
        </span>
        <textarea
          data-testid={ids.statusEditLine}
          rows={2}
          value={draft.statusLine}
          onChange={(event) => setField({ statusLine: event.target.value })}
          className={`${textareaClass} min-h-0 font-[family-name:var(--font-sans)]`}
        />
      </label>
      <p className="text-[length:var(--text-xs)] text-ink-muted">
        Saving sets Verified at to now. The old line stays in History.
      </p>
      <div className="flex items-center gap-3">
        <button
          type="button"
          data-testid={ids.statusEditSave}
          className="h-7 rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] hover:bg-signal-soft"
          onClick={onDone}
        >
          Done editing
        </button>
        <button
          type="button"
          data-testid={ids.statusEditCancel}
          className="h-7 rounded-control px-3 text-[length:var(--text-sm)] text-ink-muted hover:bg-signal-soft"
          onClick={onCancel}
        >
          Cancel
        </button>
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          Save in the header writes it with any other changes.
        </span>
      </div>
    </div>
  );
}
