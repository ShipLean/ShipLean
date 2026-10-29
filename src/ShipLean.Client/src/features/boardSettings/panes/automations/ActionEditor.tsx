import type { ActionJson, TransitionJson } from '../../../../api/persisted';
import { boardSettingsTestIds as ids } from '../../testIds';
import { ActionRow } from './ActionRow';
import { newActionId } from './transitionEdits';
import type { AgentConnections } from './useAgentConnections';

/**
 * A transition's actions. The last one can't be removed: the server wants at least one, so the button is disabled and
 * says why.
 */
export function ActionEditor({
  transition,
  connections,
  readOnly,
  onChange,
}: {
  transition: TransitionJson;
  connections: AgentConnections;
  readOnly: boolean;
  onChange(next: TransitionJson): void;
}) {
  const { actions } = transition;
  const set = (next: ActionJson[]) => onChange({ ...transition, actions: next });
  const only = actions.length <= 1;
  return (
    <div className="grid gap-2">
      {actions.map((action) => (
        <ActionRow
          key={action.id}
          action={action}
          connections={connections}
          readOnly={readOnly}
          canRemove={!only}
          onChange={(next) => set(actions.map((a) => (a.id === next.id ? next : a)))}
          onRemove={() => set(actions.filter((a) => a.id !== action.id))}
        />
      ))}
      <div className="flex items-center gap-2">
        <button
          type="button"
          data-testid={ids.actionAdd}
          disabled={readOnly}
          className="h-6 rounded-control border border-edge px-2 text-[length:var(--text-sm)] hover:bg-sunken disabled:cursor-default disabled:opacity-35"
          onClick={() =>
            set([...actions, { id: newActionId(), kind: 'agent', prompt: '', connectionId: null }])
          }
        >
          Add Send to agent action
        </button>
        {only ? (
          <span
            data-testid={ids.actionLastHint}
            className="text-[length:var(--text-xs)] text-ink-muted"
          >
            A transition keeps at least one action. Delete the transition to remove it.
          </span>
        ) : null}
      </div>
    </div>
  );
}
