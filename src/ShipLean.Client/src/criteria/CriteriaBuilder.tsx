import { useState } from 'react';
import type { CriteriaValidationError } from '../api/criteria';
import type { CriteriaClause, CriteriaJson, CriteriaMatch, FieldId } from '../api/persisted';
import { criteriaTestIds } from '../features/criteria/testIds';
import { CriteriaChips } from './CriteriaChips';
import { ClauseRow } from './ClauseRow';
import { newClause } from './defaultClause';
import { CRITERIA_FIELDS } from './fieldDefinition';
import { checkClause } from './validate';
import type { PickLists } from './valueOptions';

const MATCHES: readonly { id: CriteriaMatch; label: string; testId: string }[] = [
  { id: 'all', label: 'All', testId: criteriaTestIds.matchAll },
  { id: 'any', label: 'Any', testId: criteriaTestIds.matchAny },
];

/** The first field offered for a new clause: a type when the caller allows it, because its values are a fixed, always valid set. */
function startField(fields: readonly FieldId[]): FieldId {
  return fields.includes('type') ? 'type' : fields[0];
}

let nextRowId = 0;
const newRowId = () => `row-${nextRowId++}`;

/**
 * The shared criteria editor: `field · operator · value` clauses joined by All or Any. Controlled and fetch-free: the caller owns
 * the value, passes the people, states and sprints a clause can pick, and feeds back the server's `errors`. A clause the
 * local check rejects shows its error too, so a half-typed clause is flagged before it is saved. `onChange` may emit criteria with
 * no clauses (the last one removed); the caller decides whether that means "no criteria".
 */
export function CriteriaBuilder({
  value,
  onChange,
  fields = CRITERIA_FIELDS,
  errors = [],
  users,
  states,
  sprints,
  showChips = true,
}: PickLists & {
  value: CriteriaJson;
  onChange: (value: CriteriaJson) => void;
  fields?: readonly FieldId[];
  errors?: readonly CriteriaValidationError[];
  /** Whether the builder shows its own summary chips; turn it off where the chips already sit in a toolbar. */
  showChips?: boolean;
}) {
  // A clause has no identity in the criteria, so each row gets one here, kept in step with the clauses: it survives a removal,
  // which keeps the rows' own state (a half-typed list) with the right clause. A change from outside that alters the count starts over.
  const [rowIds, setRowIds] = useState(() => value.clauses.map(newRowId));
  let ids = rowIds;
  if (ids.length !== value.clauses.length) {
    ids = value.clauses.map(newRowId);
    setRowIds(ids);
  }
  const lists: PickLists = { users, states, sprints };
  const replaceClause = (index: number, clause: CriteriaClause) =>
    onChange({ ...value, clauses: value.clauses.map((c, i) => (i === index ? clause : c)) });
  const removeClause = (index: number) => {
    setRowIds(ids.filter((_, i) => i !== index));
    onChange({ ...value, clauses: value.clauses.filter((_, i) => i !== index) });
  };

  const errorsOf = (index: number): CriteriaValidationError[] => {
    const found = errors.filter((e) => e.clauseIndex === index);
    const local = checkClause(index, value.clauses[index]);
    return local && !found.some((e) => e.message === local.message) ? [...found, local] : found;
  };
  const documentErrors = errors.filter((e) => e.clauseIndex === null);

  return (
    <div data-testid={criteriaTestIds.builder} className="grid gap-2">
      {value.clauses.length > 1 ? (
        <div className="flex items-center gap-2 text-[length:var(--text-xs)] text-ink-muted">
          Match
          <div
            role="group"
            aria-label="Match"
            data-testid={criteriaTestIds.match}
            className="inline-flex overflow-hidden rounded-control border border-edge"
          >
            {MATCHES.map((option) => (
              <button
                key={option.id}
                type="button"
                data-testid={option.testId}
                aria-pressed={value.match === option.id}
                className="px-2.5 py-0.5 aria-pressed:bg-signal-soft aria-pressed:font-medium aria-pressed:text-ink"
                onClick={() => onChange({ ...value, match: option.id })}
              >
                {option.label}
              </button>
            ))}
          </div>
        </div>
      ) : null}
      {value.clauses.map((clause, index) => (
        <ClauseRow
          key={ids[index]}
          index={index}
          clause={clause}
          match={value.match}
          fields={fields}
          lists={lists}
          errors={errorsOf(index)}
          onChange={(next) => replaceClause(index, next)}
          onRemove={() => removeClause(index)}
        />
      ))}
      {documentErrors.map((error) => (
        <p
          key={error.message}
          role="alert"
          data-testid={criteriaTestIds.documentError}
          className="text-[length:var(--text-xs)] text-alert"
        >
          {error.message}
        </p>
      ))}
      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          data-testid={criteriaTestIds.add}
          className="btn-ghost"
          onClick={() => {
            setRowIds([...ids, newRowId()]);
            onChange({
              ...value,
              clauses: [...value.clauses, newClause(startField(fields), lists)],
            });
          }}
        >
          + Add clause
        </button>
        {showChips && value.clauses.length > 0 ? <CriteriaChips value={value} {...lists} /> : null}
      </div>
    </div>
  );
}
