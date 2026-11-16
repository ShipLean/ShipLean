import type { SprintFormValues } from '../form/SprintForm';

const FIELD_NAMES: Record<keyof SprintFormValues, string> = {
  name: 'name',
  startDate: 'start date',
  endDate: 'end date',
  goal: 'goal',
};

/** The form's fields in the order they show. */
export const SPRINT_FIELDS = Object.keys(FIELD_NAMES) as (keyof SprintFormValues)[];

/** The Save bar's line for Edit sprint: what has changed, as "End date changed" or "Name, end date and goal changed". */
export function changedSummary(fields: (keyof SprintFormValues)[]): string {
  if (fields.length === 0) return '';
  const names = fields.map((field) => FIELD_NAMES[field]);
  const list =
    names.length === 1
      ? names[0]
      : `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`;
  return `${list.charAt(0).toUpperCase()}${list.slice(1)} changed`;
}
