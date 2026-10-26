import type { FieldDefinition, CriteriaOp } from '../api/persisted';
import type { SprintRef, StateRef, UserRef } from '../api/refs';
import { ME } from './fieldDefinition';

/** What the caller knows about the people, states and sprints a clause can pick; the builder never fetches them. */
export interface PickLists {
  users?: readonly UserRef[];
  states?: readonly StateRef[];
  sprints?: readonly SprintRef[];
}

export interface ValueOption {
  value: string | number;
  label: string;
}

/** The values a clause on this field can pick from, or null when the value is typed (text, dates, ids with no list given). */
export function valueOptions(
  field: FieldDefinition,
  op: CriteriaOp,
  lists: PickLists,
): ValueOption[] | null {
  if (op === 'contains' || op === 'notContains') return null;
  switch (field.kind) {
    case 'user':
      return [
        { value: ME, label: 'Me' },
        ...(lists.users ?? []).map((u) => ({ value: u.id, label: u.name })),
      ];
    case 'state':
      return lists.states ? lists.states.map((s) => ({ value: s.id, label: s.name })) : null;
    case 'sprint':
      return lists.sprints ? lists.sprints.map((s) => ({ value: s.id, label: s.name })) : null;
    default:
      return field.values ? field.values.map((v) => ({ value: v.value, label: v.label })) : null;
  }
}
