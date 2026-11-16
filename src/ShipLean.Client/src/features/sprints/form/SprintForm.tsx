import type { FormEvent, ReactNode, Ref } from 'react';
import { useTouchShell } from '../../../touch/useTouchShell';
import type { SprintCreate } from '@shiplean/core/api/sprints';

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
  /** The Name input, for a form that takes focus when it opens. */
  nameRef?: Ref<HTMLInputElement>;
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
  /** Leaves out the button row, for a host that submits from elsewhere (the full-screen dialog's header). */
  hideSubmit?: boolean;
  /** The error is about the dates: at 900px and below they are marked invalid and the error shows under them. */
  datesError?: boolean;
  /** At 900px and below, the line under Start and End while there is no error (and the dates are not locked). */
  datesNote?: ReactNode;
}

const LABEL = 'text-[length:var(--text-sm)] text-ink-muted';

/** Name, Start, End and Goal with the submit button and the error line: the body of the New sprint and Edit sprint rails. */
export function SprintForm(props: SprintFormProps) {
  const { touch } = useTouchShell();
  if (touch) {
    return <TouchSprintForm {...props} />;
  }
  const {
    idPrefix,
    ids,
    values,
    nameRef,
    onChange,
    onSubmit,
    submitLabel,
    pending,
    error,
    datesLocked = false,
    datesHint,
    onCancel,
    hideSubmit = false,
  } = props;
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
        ref={nameRef}
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
      {hideSubmit ? null : (
        <>
          <span />
          <div className="flex justify-end gap-2">
            {onCancel !== undefined ? (
              <button
                type="button"
                data-testid={ids.cancel}
                className="btn-ghost"
                onClick={onCancel}
              >
                Cancel
              </button>
            ) : null}
            <button
              type="submit"
              data-testid={ids.submit}
              className="btn-signal"
              disabled={pending}
            >
              {submitLabel}
            </button>
          </div>
        </>
      )}
    </form>
  );
}

const TOUCH_LABEL = 'grid gap-1 text-[length:var(--text-sm)] text-ink-muted';
const NOTE = 'm-0 text-[length:var(--text-sm)] text-ink-muted';

/**
 * The form at 900px and below (SHIP-724 D9, SHIP-723 P5 designs): labels above full-width fields, Start and End side by
 * side, the dates' note or error under them, Goal as a three-line textarea. The error line stays last in the DOM (a host
 * with no button row ends on it) and is placed under the dates with `order`.
 */
function TouchSprintForm({
  idPrefix,
  ids,
  values,
  nameRef,
  onChange,
  onSubmit,
  submitLabel,
  pending,
  error,
  datesLocked = false,
  datesHint,
  onCancel,
  hideSubmit = false,
  datesError = false,
  datesNote,
}: SprintFormProps) {
  const invalidDates = datesError && error !== '';
  const dateField = `field w-full min-w-0 touch:h-10 ${invalidDates ? 'border-alert' : ''}`;
  const note =
    datesLocked && datesHint !== undefined ? (
      <p data-testid={ids.datesHint} className={`${NOTE} order-3`}>
        {datesHint}
      </p>
    ) : datesNote !== undefined && error === '' ? (
      <p className={`${NOTE} order-3`}>{datesNote}</p>
    ) : null;
  return (
    <form className="flex flex-col gap-3" onSubmit={onSubmit}>
      <label htmlFor={`${idPrefix}-name`} className={`${TOUCH_LABEL} order-1`}>
        Name
        <input
          id={`${idPrefix}-name`}
          ref={nameRef}
          data-testid={ids.name}
          className="field w-full text-ink touch:h-10"
          value={values.name}
          onChange={(event) => onChange('name', event.target.value)}
        />
      </label>
      <div className="order-2 grid grid-cols-2 gap-3">
        <label htmlFor={`${idPrefix}-start`} className={TOUCH_LABEL}>
          Start
          <input
            id={`${idPrefix}-start`}
            data-testid={ids.start}
            type="date"
            aria-invalid={invalidDates}
            className={`${dateField} text-ink`}
            value={values.startDate}
            disabled={datesLocked}
            onChange={(event) => onChange('startDate', event.target.value)}
          />
        </label>
        <label htmlFor={`${idPrefix}-end`} className={TOUCH_LABEL}>
          End
          <input
            id={`${idPrefix}-end`}
            data-testid={ids.end}
            type="date"
            aria-invalid={invalidDates}
            className={`${dateField} text-ink`}
            value={values.endDate}
            disabled={datesLocked}
            onChange={(event) => onChange('endDate', event.target.value)}
          />
        </label>
      </div>
      {note}
      <label htmlFor={`${idPrefix}-goal`} className={`${TOUCH_LABEL} order-5`}>
        Goal
        <textarea
          id={`${idPrefix}-goal`}
          data-testid={ids.goal}
          rows={3}
          className="field min-h-[76px] w-full resize-y py-2 leading-snug text-ink"
          placeholder="Optional"
          value={values.goal}
          onChange={(event) => onChange('goal', event.target.value)}
        />
      </label>
      {hideSubmit ? null : (
        <div className="order-6 flex justify-end gap-2">
          {onCancel !== undefined ? (
            <button
              type="button"
              data-testid={ids.cancel}
              className="btn-ghost h-[var(--touch-target)]"
              onClick={onCancel}
            >
              Cancel
            </button>
          ) : null}
          <button
            type="submit"
            data-testid={ids.submit}
            className="btn-signal h-[var(--touch-target)]"
            disabled={pending}
          >
            {submitLabel}
          </button>
        </div>
      )}
      <span
        data-testid={ids.error}
        role="alert"
        className={`text-[length:var(--text-sm)] text-alert empty:hidden ${invalidDates ? 'order-3' : 'order-4'}`}
      >
        {error}
      </span>
    </form>
  );
}
