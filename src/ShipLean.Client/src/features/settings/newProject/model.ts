import type { ProjectCreate } from '../../../api/projects';
import type { SprintCreate } from '../../../api/sprints';
import type { UserRef } from '../../../api/refs';

export interface NewProjectMember {
  email: string;
  user: UserRef;
}

export interface NewProjectForm {
  name: string;
  /** What the person typed; ignored until they edit the key (the key then follows the name's suggestion). */
  typedKey: string;
  keyEdited: boolean;
  from: 'defaults' | 'copy';
  copyFromId: string | null;
  members: NewProjectMember[];
  workingDays: number[];
  sprint: boolean;
  /** yyyy-MM-dd, exactly as the date input holds it: the browser never reads it as a date. Used once the person has edited a date. */
  start: string;
  end: string;
  /** Until a date is edited, Sprint 1 follows the server's defaults for the working days. */
  datesEdited: boolean;
}

export const emptyForm: NewProjectForm = {
  name: '',
  typedKey: '',
  keyEdited: false,
  from: 'defaults',
  copyFromId: null,
  members: [],
  workingDays: [1, 2, 3, 4, 5],
  sprint: true,
  start: '',
  end: '',
  datesEdited: false,
};

/** Initials of several words, the first four letters of one; empty when the name has no letters. */
export function suggestKey(name: string): string {
  const words = name
    .split(/[^A-Za-z]+/)
    .filter((w) => w !== '')
    .map((w) => w.toUpperCase());
  if (words.length === 0) return '';
  const initials = words.map((w) => w[0]).join('');
  const key = words.length > 1 ? initials : words[0];
  return key.slice(0, 4);
}

export const keyOf = (form: NewProjectForm) =>
  form.keyEdited ? form.typedKey : suggestKey(form.name);

export const keyHasLength = (key: string) => key.length >= 2 && key.length <= 4;
export const keyHasOnlyLetters = (key: string) => /^[A-Z]+$/.test(key);
export const keyIsWellFormed = (key: string) => keyHasLength(key) && keyHasOnlyLetters(key);

/** The first reason Create is not yet possible, or null. The key's availability is the server's answer. */
export function blocker(
  form: NewProjectForm,
  keyAvailable: boolean,
  sprintName: string | undefined,
): string | null {
  if (form.name.trim() === '') return 'Give the project a name';
  if (!keyAvailable) return 'Fix the key to continue';
  if (form.workingDays.length === 0) return 'Pick at least one working day';
  if (form.sprint && (sprintName === undefined || form.start === '' || form.end === ''))
    return 'Pick the dates of Sprint 1';
  if (form.sprint && form.end < form.start) return 'Sprint 1 cannot end before it starts';
  if (form.from === 'copy' && form.copyFromId === null) return 'Pick a project to copy';
  return null;
}

/** The form with Sprint 1's dates filled from the server's defaults until the person edits one. */
export function withSprintDefaults(
  form: NewProjectForm,
  defaults: SprintCreate | undefined,
): NewProjectForm {
  if (form.datesEdited || defaults === undefined) return form;
  return { ...form, start: defaults.startDate, end: defaults.endDate };
}

export function toCreate(form: NewProjectForm, sprintName: string | undefined): ProjectCreate {
  return {
    name: form.name.trim(),
    key: keyOf(form),
    copyFromProjectId: form.from === 'copy' ? form.copyFromId : null,
    memberEmails: form.members.map((m) => m.email),
    workingDays: form.workingDays,
    firstSprint:
      form.sprint && sprintName !== undefined
        ? { name: sprintName, startDate: form.start, endDate: form.end, goal: '' }
        : null,
  };
}
