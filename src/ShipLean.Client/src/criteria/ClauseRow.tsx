import { useId } from 'react';
import type { CriteriaValidationError } from '../api/criteria';
import type { CriteriaClause, CriteriaMatch, CriteriaOp, FieldId } from '../api/persisted';
import { criteriaTestIds } from '../features/criteria/testIds';
import { clauseText, opLabel } from './clauseText';
import { ClauseValue } from './ClauseValue';
import { defaultValue } from './defaultClause';
import { criteriaField } from './fieldDefinition';
import type { PickLists } from './valueOptions';

/** One `field · operator · value` row with its lead word (Where / AND / OR), a remove button and the errors found on it. */
export function ClauseRow({
  index,
  clause,
  match,
  fields,
  lists,
  errors,
  onChange,
  onRemove,
}: {
  index: number;
  clause: CriteriaClause;
  match: CriteriaMatch;
  fields: readonly FieldId[];
  lists: PickLists;
  errors: readonly CriteriaValidationError[];
  onChange: (clause: CriteriaClause) => void;
  onRemove: () => void;
}) {
  const field = criteriaField(clause.field);
  const errorId = useId();
  const errorIds = errors.map((_, i) => `${errorId}-${i}`);
  const marksFor = (...codes: string[]) => {
    const mine = errors
      .map((e, i) => ({ code: e.code, id: errorIds[i] }))
      .filter((e) => codes.includes(e.code));
    return {
      'aria-invalid': mine.length > 0,
      'aria-describedby': mine.length > 0 ? mine.map((e) => e.id).join(' ') : undefined,
    };
  };
  const valueErrors = errors
    .map((e, i) => ({ code: e.code, id: errorIds[i] }))
    .filter((e) => e.code !== 'unknownField' && e.code !== 'operatorNotAllowed');
  const lead = index === 0 ? 'Where' : match === 'all' ? 'AND' : 'OR';

  const changeField = (id: string) => {
    const next = criteriaField(id);
    if (!next) return;
    const op = next.ops[0];
    onChange({ field: next.id, op, value: defaultValue(next, op, lists) });
  };
  const changeOp = (op: CriteriaOp) => {
    if (field) onChange({ ...clause, op, value: defaultValue(field, op, lists) });
  };

  return (
    <div data-testid={criteriaTestIds.clause} data-invalid={errors.length > 0}>
      <div className="grid grid-cols-[3rem_10rem_8rem_minmax(0,1fr)_1.5rem] items-center gap-1.5">
        <span className="text-[length:var(--text-xs)] font-medium text-ink-muted">{lead}</span>
        <select
          data-testid={criteriaTestIds.field}
          className="field"
          aria-label="Field"
          {...marksFor('unknownField')}
          value={clause.field}
          onChange={(event) => changeField(event.target.value)}
        >
          {!fields.some((f) => f === clause.field) ? (
            <option value={clause.field}>{clause.field}</option>
          ) : null}
          {fields.map((id) => (
            <option key={id} value={id}>
              {criteriaField(id)?.label ?? id}
            </option>
          ))}
        </select>
        <select
          data-testid={criteriaTestIds.op}
          className="field"
          aria-label="Operator"
          {...marksFor('operatorNotAllowed')}
          value={clause.op}
          onChange={(event) => changeOp(event.target.value as CriteriaOp)}
        >
          {!field?.ops.includes(clause.op) ? (
            <option value={clause.op}>{opLabel(clause.op, field?.kind ?? 'text')}</option>
          ) : null}
          {(field?.ops ?? []).map((op) => (
            <option key={op} value={op}>
              {opLabel(op, field?.kind ?? 'text')}
            </option>
          ))}
        </select>
        <div className="min-w-0">
          {field ? (
            <ClauseValue
              clause={clause}
              field={field}
              lists={lists}
              invalid={valueErrors.length > 0}
              describedBy={
                valueErrors.length > 0 ? valueErrors.map((e) => e.id).join(' ') : undefined
              }
              onChange={(value) => onChange({ ...clause, value })}
            />
          ) : null}
        </div>
        <button
          type="button"
          data-testid={criteriaTestIds.remove}
          aria-label={`Remove clause ${index + 1}: ${clauseText(clause, lists)}`}
          className="h-6 w-6 rounded-sm text-ink-muted hover:bg-sunken hover:text-ink"
          onClick={onRemove}
        >
          ✕
        </button>
      </div>
      {errors.map((error, i) => (
        <p
          key={error.message}
          id={errorIds[i]}
          role="alert"
          data-testid={criteriaTestIds.error}
          className="mt-1 ml-[3.375rem] text-[length:var(--text-xs)] text-alert"
        >
          {error.message}
        </p>
      ))}
    </div>
  );
}
