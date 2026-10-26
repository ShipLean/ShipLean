import type {
  ProjectRole,
  ProjectSettingsDto,
  ProjectSettingsPut,
  StateChanges,
  StateDto,
} from '../../../api/projects';
import { WORK_ITEM_TYPE_IDS, type UserRef, type WorkItemTypeId } from '../../../api/refs';
import { TYPE_LABELS } from '../states/categories';
import { rebaseStates, removeLabel, rowsOf, typeProblems } from '../states/stateModel';

export type DraftTab = 'general' | 'states' | 'members' | 'sprints';

export interface DraftMember {
  email: string;
  user: UserRef;
  role: ProjectRole;
  /** Staged for removal: shown struck through with Undo, left out of the save. */
  removed: boolean;
  /** Added in this draft and not saved yet: Remove drops the row instead of striking it through. */
  fresh: boolean;
}

/** Every tab's edits as one draft; text fields stay text until saved. States are staged by the States tab. */
export interface SettingsDraft {
  name: string;
  description: string;
  /** ISO weekdays, ascending. */
  workingDays: number[];
  members: DraftMember[];
  velocityWindow: string;
  velocityOverride: string;
  states: StateChanges;
}

/** A state row's name is `state-<id>`; a type's picker button, which a type-wide problem points at, is `stateType-<type>`. */
export type DraftField =
  | 'name'
  | 'workingDays'
  | 'velocityWindow'
  | 'velocityOverride'
  | `state-${string}`
  | `stateType-${WorkItemTypeId}`;

export interface FieldError {
  tab: DraftTab;
  field: DraftField;
  message: string;
  /** The work item type a States tab error is about. */
  type?: WorkItemTypeId;
}

export interface Change {
  tab: DraftTab;
  label: string;
  /** The member a members-tab change is about. */
  memberId?: string;
}

const noStateChanges = (): StateChanges => ({ add: [], update: [], reorder: [], remove: [] });

export function toDraft(settings: ProjectSettingsDto): SettingsDraft {
  return {
    name: settings.general.name,
    description: settings.general.description,
    workingDays: [...settings.general.workingDays].sort((a, b) => a - b),
    members: settings.members.map((m) => ({
      email: m.email,
      user: m.user,
      role: m.role,
      removed: false,
      fresh: false,
    })),
    velocityWindow: String(settings.sprints.velocityWindow),
    velocityOverride:
      settings.sprints.velocityOverride === null ? '' : String(settings.sprints.velocityOverride),
    states: noStateChanges(),
  };
}

const OVERRIDE_PATTERN = /^\d+(\.\d{1,2})?$/;
const WINDOW_PATTERN = /^\d+$/;

function parseWindow(text: string): number | undefined {
  const trimmed = text.trim();
  return WINDOW_PATTERN.test(trimmed) && Number(trimmed) >= 1 ? Number(trimmed) : undefined;
}

function parseOverride(text: string): number | null | undefined {
  const trimmed = text.trim();
  if (trimmed === '') return null;
  return OVERRIDE_PATTERN.test(trimmed) ? Number(trimmed) : undefined;
}

export interface ParsedDraft {
  velocityWindow: number;
  velocityOverride: number | null;
}

/** The rules the server enforces on the tabs this draft owns, so Save can jump to the field before sending anything. */
export function parse(
  draft: SettingsDraft,
  states: StateDto[],
): { errors: FieldError[] } | { parsed: ParsedDraft } {
  const errors: FieldError[] = [];
  if (draft.name.trim() === '') {
    errors.push({ tab: 'general', field: 'name', message: 'A project needs a name.' });
  }
  if (draft.workingDays.length === 0) {
    errors.push({
      tab: 'general',
      field: 'workingDays',
      message: 'Pick at least one working day.',
    });
  }
  const velocityWindow = parseWindow(draft.velocityWindow);
  if (velocityWindow === undefined) {
    errors.push({
      tab: 'sprints',
      field: 'velocityWindow',
      message: 'The velocity window is a whole number of sprints, at least 1.',
    });
  }
  const velocityOverride = parseOverride(draft.velocityOverride);
  if (velocityOverride === undefined) {
    errors.push({
      tab: 'sprints',
      field: 'velocityOverride',
      message: 'The override is zero or more points, with at most two decimal places.',
    });
  }
  errors.push(...stateErrors(states, draft.states));
  if (velocityWindow === undefined || velocityOverride === undefined || errors.length > 0) {
    return { errors };
  }
  return { parsed: { velocityWindow, velocityOverride } };
}

/** What the server refuses on each type's list: a blank or repeated name (at the row), no Proposed or Completed state (at the type). */
function stateErrors(states: StateDto[], changes: StateChanges): FieldError[] {
  return WORK_ITEM_TYPE_IDS.flatMap((type) => {
    const { rowErrors, typeError } = typeProblems(rowsOf(states, changes, type));
    const rows = [...rowErrors].map(([id, message]): FieldError => ({
      tab: 'states',
      field: `state-${id}`,
      message,
      type,
    }));
    return typeError === undefined
      ? rows
      : [...rows, { tab: 'states', field: `stateType-${type}`, message: typeError, type }];
  });
}

export function validate(draft: SettingsDraft, states: StateDto[]): FieldError[] {
  const result = parse(draft, states);
  return 'errors' in result ? result.errors : [];
}

/** The body of the one save, from the values `parse` accepted. */
export function toPut(draft: SettingsDraft, parsed: ParsedDraft): ProjectSettingsPut {
  return {
    general: {
      name: draft.name.trim(),
      description: draft.description,
      workingDays: draft.workingDays,
    },
    members: draft.members.filter((m) => !m.removed).map((m) => ({ email: m.email, role: m.role })),
    sprints: {
      velocityWindow: parsed.velocityWindow,
      velocityOverride: parsed.velocityOverride,
    },
    states: draft.states,
  };
}

/**
 * A teammate saved while this draft had edits: their document becomes the base, and only this user's own edits are
 * replayed on top (changed fields, member adds, removes and role changes by user id, the staged state changes).
 */
export function rebase(
  latest: ProjectSettingsDto,
  base: SettingsDraft,
  draft: SettingsDraft,
  baseStates: StateDto[],
): { base: SettingsDraft; draft: SettingsDraft } {
  const next = toDraft(latest);
  const mine = <K extends keyof SettingsDraft>(key: K): SettingsDraft[K] =>
    JSON.stringify(base[key]) === JSON.stringify(draft[key]) ? next[key] : draft[key];
  const members = next.members.map((row) => {
    const was = base.members.find((m) => m.user.id === row.user.id);
    const now = draft.members.find((m) => m.user.id === row.user.id);
    if (was === undefined || now === undefined) return row;
    return {
      ...row,
      removed: now.removed,
      role: was.role === now.role ? row.role : now.role,
    };
  });
  for (const added of draft.members) {
    if (!base.members.some((m) => m.user.id === added.user.id)) {
      if (!members.some((m) => m.user.id === added.user.id)) members.push(added);
    }
  }
  return {
    base: next,
    draft: {
      name: mine('name'),
      description: mine('description'),
      workingDays: mine('workingDays'),
      velocityWindow: mine('velocityWindow'),
      velocityOverride: mine('velocityOverride'),
      members,
      states: rebaseStates(latest.states, baseStates, draft.states),
    },
  };
}

/** Every difference from the saved draft, one entry each, for the count and the tab dots. */
export function changesBetween(
  base: SettingsDraft,
  draft: SettingsDraft,
  states: StateDto[],
): Change[] {
  const changes: Change[] = [];
  if (base.name !== draft.name) changes.push({ tab: 'general', label: 'Name' });
  if (base.description !== draft.description)
    changes.push({ tab: 'general', label: 'Description' });
  if (base.workingDays.join() !== draft.workingDays.join()) {
    changes.push({ tab: 'general', label: 'Working days' });
  }
  for (const member of draft.members) {
    const saved = base.members.find((m) => m.user.id === member.user.id);
    const memberId = member.user.id;
    if (saved === undefined) {
      changes.push({ tab: 'members', label: `Add ${member.email}`, memberId });
    } else if (member.removed) {
      changes.push({ tab: 'members', label: `Remove ${member.email}`, memberId });
    } else if (saved.role !== member.role) {
      changes.push({ tab: 'members', label: `Role of ${member.email}`, memberId });
    }
  }
  if (base.velocityWindow !== draft.velocityWindow) {
    changes.push({ tab: 'sprints', label: 'Velocity window' });
  }
  if (base.velocityOverride !== draft.velocityOverride) {
    changes.push({ tab: 'sprints', label: 'Velocity override' });
  }
  const { add, update, reorder, remove } = draft.states;
  for (const state of add) changes.push({ tab: 'states', label: `Add state ${state.name}` });
  for (const state of update) changes.push({ tab: 'states', label: `Edit state ${state.name}` });
  for (const order of reorder)
    changes.push({ tab: 'states', label: `Reorder ${TYPE_LABELS[order.type]} states` });
  for (const state of remove) {
    changes.push({ tab: 'states', label: removeLabel(states, draft.states, state.stateId) });
  }
  return changes;
}

/** Whether two saved documents differ in anything but how many items sit in each state (those refresh on work item events). */
export function sameSettings(a: ProjectSettingsDto, b: ProjectSettingsDto): boolean {
  const flat = (s: ProjectSettingsDto) =>
    JSON.stringify({
      ...s,
      states: s.states.map((x) => [x.id, x.type, x.name, x.category, x.order]),
    });
  return flat(a) === flat(b);
}
