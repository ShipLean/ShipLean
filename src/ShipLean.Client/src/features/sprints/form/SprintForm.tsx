import type { FormEvent } from 'react';
import type { SprintCreate } from '../../../api/sprints';

/** The four fields a sprint is made of, as the form holds them (dates as `yyyy-MM-dd`). */
export type SprintFormValues = SprintCreate;

/** The test ids of one use of the form (New sprint and Edit sprint each have their own). */
export interface SprintFormIds {
  name: string;
  start: string;
  end: string;
  goal: string;
  submit: string;
  error: string;
  cancel?: string;
  datesHint?: string;
}

interface SprintFormProps {
  /** Prefix for the inputs' DOM ids, so two forms on a page keep their labels apart. */
  idPrefix: string;
  ids: SprintFormIds;
  values: SprintFormValues;
  onChange: (field: keyof SprintFormValues, value: string) => void;
  onSubmit: (event: FormEvent) => void;
  submitLabel: string;
  pending: boolean;
  /** Shown beside the button (a client check or a server refusal); empty shows nothing. */
  error: string;
  /** Start and End are shown but can't be typed in; `datesHint` says why. */
  datesLocked?: boolean;
  datesHint?: string;
  /** When given, a Cancel button sits beside the submit button. */
  onCancel?: () => void;
}

const LABEL = 'text-[length:var(--text-sm)] text-ink-muted';

/** Name, Start, End and Goal with the submit button and the error line: the body of the New sprint and Edit sprint rails. */
export function SprintForm({
  idPrefix,
  ids,
  values,
  onChange,
  onSubmit,
  submitLabel,
  pending,
  error,
  datesLocked = false,
  datesHint,
  onCancel,
}: SprintFormProps) {
  return (
    <form
      className="grid grid-cols-[auto_minmax(0,1fr)] items-center gap-x-3 gap-y-2"
      onSubmit={onSubmit}
    >
      <label htmlFor={`${idPrefix}-name`} className={LABEL}>
        Name
      </label>
      <input
        id={`${idPrefix}-name`}
        data-testid={ids.name}
        className="field w-full"
        value={values.name}
        onChange={(event) => onChange('name', event.target.value)}
      />
      <label htmlFor={`${idPrefix}-start`} className={LABEL}>
        Start
      </label>
      <input
        id={`${idPrefix}-start`}
        data-testid={ids.start}
        type="date"
        className="field w-full"
        value={values.startDate}
        disabled={datesLocked}
        onChange={(event) => onChange('startDate', event.target.value)}
      />
      <label htmlFor={`${idPrefix}-end`} className={LABEL}>
        End
      </label>
      <input
        id={`${idPrefix}-end`}
        data-testid={ids.end}
        type="date"
        className="field w-full"
        value={values.endDate}
        disabled={datesLocked}
        onChange={(event) => onChange('endDate', event.target.value)}
      />
      {datesLocked && datesHint !== undefined ? (
        <>
          <span />
          <p
            data-testid={ids.datesHint}
            className="m-0 text-[length:var(--text-xs)] text-ink-muted"
          >
            {datesHint}
          </p>
        </>
      ) : null}
      <label htmlFor={`${idPrefix}-goal`} className={LABEL}>
        Goal
      </label>
      <textarea
        id={`${idPrefix}-goal`}
        data-testid={ids.goal}
        rows={2}
        className="field w-full"
        placeholder="Optional"
        value={values.goal}
        onChange={(event) => onChange('goal', event.target.value)}
      />
      <span />
      <span
        data-testid={ids.error}
        role="alert"
        className="text-[length:var(--text-sm)] text-alert"
      >
        {error}
      </span>
      <span />
      <div className="flex justify-end gap-2">
        {onCancel !== undefined ? (
          <button type="button" data-testid={ids.cancel} className="btn-ghost" onClick={onCancel}>
            Cancel
          </button>
        ) : null}
        <button type="submit" data-testid={ids.submit} className="btn-signal" disabled={pending}>
          {submitLabel}
        </button>
      </div>
    </form>
  );
}
