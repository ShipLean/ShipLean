import type { BoardLevel, BoardTypeStatesDto } from '../../../../api/boards';
import type { StateRefJson, TransitionJson } from '../../../../api/persisted';
import type { StateRef, WorkItemTypeId } from '../../../../api/refs';

export const TRANSITION_TYPE_LABELS: Partial<Record<WorkItemTypeId, string>> = {
  story: 'Story',
  bug: 'Bug',
  feature: 'Feature',
};

/** The types a board of this level may define transitions for: the server refuses any other. */
export function transitionTypesFor(level: BoardLevel): WorkItemTypeId[] {
  if (level === 'stories') return ['story', 'bug'];
  if (level === 'features') return ['feature'];
  return [];
}

export const newTransitionId = () => `tr-${crypto.randomUUID().slice(0, 8)}`;
export const newActionId = () => `act-${crypto.randomUUID().slice(0, 8)}`;

/** The states of one type a transition can name: every one in context.states (the server already left out archived ones), in state order. */
export function liveStatesOf(states: BoardTypeStatesDto[], type: WorkItemTypeId): StateRef[] {
  const entry = states.find((s) => s.type === type);
  return entry?.states ?? [];
}

const sameRef = (a: StateRefJson | null, b: StateRefJson | null) => a?.stateId === b?.stateId;

/** Why a transition can't fire ("Idea was removed"), or null when every state it names is live. */
export function invalidReason(
  transition: TransitionJson,
  states: BoardTypeStatesDto[],
): string | null {
  const live = new Set(liveStatesOf(states, transition.type).map((s) => s.id));
  const gone = [transition.from, transition.to]
    .filter((ref): ref is StateRefJson => ref !== null && !live.has(ref.stateId))
    .map((ref) => ref.name);
  const unique = [...new Set(gone)];
  return unique.length === 0 ? null : `${unique.join(' and ')} was removed`;
}

export function transitionLabel(transition: TransitionJson): string {
  const type = TRANSITION_TYPE_LABELS[transition.type] ?? transition.type;
  return `${type} · ${transition.from?.name ?? 'Any state'} → ${transition.to.name}`;
}

/** One line per transition on what it does: the first action's prompt, or that it has none yet. */
export function actionSummary(transition: TransitionJson): string {
  if (transition.actions.length === 0) return 'No actions';
  return transition.actions
    .map((action) => {
      const firstLine = action.prompt.trim().split('\n')[0] ?? '';
      const prompt = firstLine === '' ? 'no prompt yet' : firstLine;
      return `Send to agent: ${prompt}`;
    })
    .join(' · ');
}

/** The reason a new transition is refused, or null: same state on both sides, or a (type, from, to) already defined. */
export function refusalFor(
  transitions: TransitionJson[],
  type: WorkItemTypeId,
  from: StateRefJson | null,
  to: StateRefJson,
): string | null {
  if (from !== null && from.stateId === to.stateId) return 'From and To must be different states.';
  const duplicate = transitions.some(
    (t) => t.type === type && sameRef(t.from, from) && t.to.stateId === to.stateId,
  );
  return duplicate ? 'That transition is already defined.' : null;
}

/** A new transition starts with one empty Send to agent action: the server wants at least one. */
export function newTransition(
  type: WorkItemTypeId,
  from: StateRefJson | null,
  to: StateRefJson,
): TransitionJson {
  return {
    id: newTransitionId(),
    type,
    from,
    to,
    actions: [{ id: newActionId(), kind: 'agent', prompt: '', connectionId: null }],
  };
}
