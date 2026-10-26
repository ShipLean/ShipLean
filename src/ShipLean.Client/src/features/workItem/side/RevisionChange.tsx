import { FIELD_IDS } from '../../../api/persisted';
import type { FieldChange } from '../../../api/persisted';
import { workItemTestIds as ids } from '../testIds';

const labelOf = (field: string): string =>
  FIELD_IDS.find((definition) => definition.id === field)?.label ?? field;

const SHORT = 60;

// A value the rail can show as it is: a number, a short string, or a list of short strings (tags). Anything else is null.
function plain(value: FieldChange['from']): string | null {
  if (value === null) return 'none';
  if (typeof value === 'number') return String(value);
  if (typeof value === 'string') return value.length <= SHORT ? value : null;
  if (Array.isArray(value) && value.every((entry) => typeof entry === 'string')) {
    const joined = value.length === 0 ? 'none' : value.join(', ');
    return joined.length <= SHORT ? joined : null;
  }
  return null;
}

// From → to for a change: the server's labels where it gave them (states, people, sprints), otherwise the values when
// they are short. Long text, checklists and links have nothing short to show: they say "changed".
function summary(change: FieldChange): string | null {
  if (change.toLabel !== null) return `${change.fromLabel ?? 'none'} → ${change.toLabel}`;
  const from = plain(change.from);
  const to = plain(change.to);
  return from !== null && to !== null ? `${from} → ${to}` : null;
}

// One field of a revision: its name and what it changed from and to.
export function RevisionChange({ change }: { change: FieldChange }) {
  const text = summary(change);
  return (
    <li
      data-testid={ids.revisionChange}
      data-field={change.field}
      className="flex flex-wrap gap-x-1.5 text-[length:var(--text-sm)]"
    >
      <span className="font-medium">{labelOf(change.field)}</span>
      <span className="text-ink-muted">{text ?? 'changed'}</span>
    </li>
  );
}
