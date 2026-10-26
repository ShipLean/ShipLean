import { useState } from 'react';
import type { CriteriaValue, FieldKind } from '../api/persisted';
import { criteriaTestIds } from '../features/criteria/testIds';

type Element = string | number | null;

function toElements(text: string, kind: FieldKind): Element[] {
  const numeric = kind === 'number' || kind === 'points';
  return text
    .split(',')
    .map((part) => part.trim())
    .filter((part) => part !== '')
    .map((part) => (numeric && part !== '' && !Number.isNaN(Number(part)) ? Number(part) : part));
}

/**
 * The `in` list of a field with no fixed values, typed as comma-separated text. The text is a local draft, committed on blur so a
 * trailing comma survives typing; it starts over whenever the committed list changes from outside (a removed clause, a reset).
 * Number and points lists commit numbers.
 */
export function FreeListInput({
  kind,
  value,
  onChange,
  invalid,
  describedBy,
}: {
  kind: FieldKind;
  value: readonly Element[];
  onChange: (value: CriteriaValue) => void;
  invalid: boolean;
  describedBy: string | undefined;
}) {
  const committed = value.join(', ');
  const [draft, setDraft] = useState(committed);
  const [seen, setSeen] = useState(committed);
  if (seen !== committed) {
    setSeen(committed);
    setDraft(committed);
  }
  return (
    <input
      data-testid={criteriaTestIds.value}
      className="field w-full"
      aria-label="Values, separated by commas"
      aria-invalid={invalid}
      aria-describedby={describedBy}
      value={draft}
      onChange={(event) => setDraft(event.target.value)}
      onBlur={() => onChange(toElements(draft, kind))}
    />
  );
}
