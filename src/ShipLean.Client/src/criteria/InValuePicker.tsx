import type { CriteriaValue } from '../api/persisted';
import { criteriaTestIds } from '../features/criteria/testIds';
import type { ValueOption } from './valueOptions';

/** The values of an `in` clause as a row of checkboxes; a nullable field also offers "None" (an empty field matches). */
export function InValuePicker({
  options,
  nullable,
  value,
  onChange,
  'aria-invalid': invalid,
  'aria-describedby': describedBy,
}: {
  options: readonly ValueOption[];
  nullable: boolean;
  value: readonly (string | number | null)[];
  onChange: (value: CriteriaValue) => void;
  'aria-invalid': boolean;
  'aria-describedby': string | undefined;
}) {
  const toggle = (element: string | number | null, on: boolean) => {
    const rest = value.filter((e) => e !== element);
    onChange(on ? [...rest, element] : rest);
  };
  return (
    <div
      data-testid={criteriaTestIds.value}
      role="group"
      aria-label="Values"
      aria-invalid={invalid}
      aria-describedby={describedBy}
      className="flex flex-wrap items-center gap-x-3 gap-y-1 text-[length:var(--text-sm)]"
    >
      {options.map((option) => (
        <label key={option.value} className="inline-flex items-center gap-1">
          <input
            type="checkbox"
            data-testid={criteriaTestIds.inOption}
            checked={value.includes(option.value)}
            onChange={(event) => toggle(option.value, event.target.checked)}
          />
          {option.label}
        </label>
      ))}
      {nullable ? (
        <label className="inline-flex items-center gap-1 text-ink-muted">
          <input
            type="checkbox"
            data-testid={criteriaTestIds.inNone}
            checked={value.includes(null)}
            onChange={(event) => toggle(null, event.target.checked)}
          />
          None
        </label>
      ) : null}
    </div>
  );
}
