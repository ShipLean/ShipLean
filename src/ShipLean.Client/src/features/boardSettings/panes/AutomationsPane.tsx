import type { BoardSettingsJson, TransitionJson } from '../../../api/persisted';
import type { PaneProps } from '../../../components/SettingsDialog/types';
import type { BoardSettingsContext } from '../panes';
import { boardSettingsTestIds as ids } from '../testIds';
import { ActionEditor } from './automations/ActionEditor';
import { useAgentConnections } from './automations/useAgentConnections';
import { AddTransitionForm } from './automations/AddTransitionForm';
import { TransitionRow } from './automations/TransitionRow';
import { invalidReason, transitionTypesFor } from './automations/transitionEdits';

/** Stories and Features boards: the state transitions that send an item to an agent. Invalid ones are kept, flagged and never fire. */
export function AutomationsPane({
  draft,
  onChange,
  readOnly,
  context,
}: PaneProps<BoardSettingsJson, BoardSettingsContext>) {
  const types = transitionTypesFor(context.level);
  const connections = useAgentConnections(context.projectId);
  const setTransition = (next: TransitionJson) =>
    onChange({
      ...draft,
      transitions: draft.transitions.map((t) => (t.id === next.id ? next : t)),
    });
  if (types.length === 0) {
    return (
      <div data-testid={ids.automationsPane}>
        <p data-testid={ids.automationsNotStored} className="m-0 text-ink-muted">
          This board has no automations.
        </p>
      </div>
    );
  }
  return (
    <div data-testid={ids.automationsPane} className="grid content-start gap-3">
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        A transition fires when a person moves an item from one state to another. Its actions run
        then. A transition that names a state that was removed is kept, marked Invalid, and never
        fires.
      </p>
      <AddTransitionForm
        types={types}
        states={context.states}
        transitions={draft.transitions}
        readOnly={readOnly}
        onAdd={(next) => onChange({ ...draft, transitions: [...draft.transitions, next] })}
      />
      {draft.transitions.length === 0 ? (
        <p data-testid={ids.automationsEmpty} className="m-0 text-ink-muted">
          No transitions yet. A transition names a state change, such as New to Active. When a
          person moves an item that way, its Send to agent actions run with the prompt you write.
        </p>
      ) : null}
      {draft.transitions.map((transition) => (
        <TransitionRow
          key={transition.id}
          transition={transition}
          invalid={invalidReason(transition, context.states)}
          readOnly={readOnly}
          actions={
            <ActionEditor
              transition={transition}
              connections={connections}
              readOnly={readOnly}
              onChange={setTransition}
            />
          }
          onDelete={() =>
            onChange({
              ...draft,
              transitions: draft.transitions.filter((t) => t.id !== transition.id),
            })
          }
        />
      ))}
    </div>
  );
}
