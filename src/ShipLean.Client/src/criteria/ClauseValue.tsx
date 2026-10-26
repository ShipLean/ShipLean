import type { CriteriaClause, CriteriaValue, FieldDefinition } from '../api/persisted';
import { criteriaTestIds } from '../features/criteria/testIds';
import { FreeListInput } from './FreeListInput';
import { InValuePicker } from './InValuePicker';
import { valueOptions, type PickLists } from './valueOptions';

const DATE_PREFIX = /^\d{4}-\d{2}-\d{2}/;

function scalar(value: CriteriaValue): string | number | null {
  return Array.isArray(value) ? null : value;
}

/** The value editor of one clause: nothing for isEmpty, checkboxes for `in`, a select for a fixed set, otherwise a typed input of the field's kind. */
export function ClauseValue({
  clause,
  field,
  lists,
  invalid,
  describedBy,
  onChange,
}: {
  clause: CriteriaClause;
  field: FieldDefinition;
  lists: PickLists;
  invalid: boolean;
  describedBy: string | undefined;
  onChange: (value: CriteriaValue) => void;
}) {
  const marks = { 'aria-invalid': invalid, 'aria-describedby': describedBy };
  const { op, value } = clause;
  if (op === 'isEmpty' || op === 'isNotEmpty') return null;
  const options = valueOptions(field, op, lists);

  if (op === 'in') {
    const list = Array.isArray(value) ? value : [];
    if (options) {
      return (
        <InValuePicker
          options={options}
          nullable={field.nullable}
          value={list}
          onChange={onChange}
          {...marks}
        />
      );
    }
    return (
      <FreeListInput
        kind={field.kind}
        value={list}
        onChange={onChange}
        invalid={invalid}
        describedBy={describedBy}
      />
    );
  }

  const current = scalar(value);
  if (options) {
    return (
      <select
        data-testid={criteriaTestIds.value}
        className="field w-full"
        aria-label="Value"
        {...marks}
        value={current === null ? '' : String(current)}
        onChange={(event) =>
          onChange(options.find((o) => String(o.value) === event.target.value)?.value ?? null)
        }
      >
        {current === null || !options.some((o) => o.value === current) ? (
          <option value="">Choose…</option>
        ) : null}
        {options.map((option) => (
          <option key={option.value} value={String(option.value)}>
            {option.label}
          </option>
        ))}
      </select>
    );
  }

  if (field.kind === 'number' || field.kind === 'points') {
    return (
      <input
        data-testid={criteriaTestIds.value}
        className="field w-full"
        type="number"
        step={field.kind === 'number' ? 1 : 'any'}
        aria-label="Value"
        {...marks}
        value={current ?? ''}
        onChange={(event) =>
          onChange(Number.isNaN(event.target.valueAsNumber) ? null : event.target.valueAsNumber)
        }
      />
    );
  }

  if (field.kind === 'date') {
    const text =
      typeof current === 'string' && DATE_PREFIX.test(current) ? current.slice(0, 10) : '';
    return (
      <input
        data-testid={criteriaTestIds.value}
        className="field w-full"
        type="date"
        aria-label="Value"
        {...marks}
        value={text}
        onChange={(event) =>
          onChange(event.target.value === '' ? '' : `${event.target.value}T00:00:00Z`)
        }
      />
    );
  }

  return (
    <input
      data-testid={criteriaTestIds.value}
      className="field w-full"
      aria-label="Value"
      {...marks}
      value={current === null ? '' : String(current)}
      onChange={(event) => onChange(event.target.value)}
    />
  );
}
