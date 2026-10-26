import { settingsTestIds } from '../testIds';
import { FieldErrorText } from './FieldErrorText';
import type { TabProps } from './tabProps';

const DAYS = [
  ['Mon', settingsTestIds.workingDay1],
  ['Tue', settingsTestIds.workingDay2],
  ['Wed', settingsTestIds.workingDay3],
  ['Thu', settingsTestIds.workingDay4],
  ['Fri', settingsTestIds.workingDay5],
  ['Sat', settingsTestIds.workingDay6],
  ['Sun', settingsTestIds.workingDay7],
] as const;

export function WorkingDays({ draft, setDraft, readOnly, errors }: TabProps) {
  const toggle = (day: number) =>
    setDraft((current) => ({
      ...current,
      workingDays: current.workingDays.includes(day)
        ? current.workingDays.filter((d) => d !== day)
        : [...current.workingDays, day].sort((a, b) => a - b),
    }));
  return (
    <div>
      <div
        id="settings-field-workingDays"
        tabIndex={-1}
        role="group"
        aria-label="Working days"
        data-testid={settingsTestIds.workingDays}
        className="flex gap-1"
      >
        {DAYS.map(([label, testId], index) => {
          const day = index + 1;
          const on = draft.workingDays.includes(day);
          return (
            <button
              key={day}
              type="button"
              data-testid={testId}
              aria-pressed={on}
              disabled={readOnly}
              className={`h-7 w-11 rounded-control border text-[length:var(--text-sm)] font-medium disabled:opacity-60 ${
                on ? 'border-signal bg-signal-soft text-ink' : 'border-edge bg-panel text-ink-muted'
              }`}
              onClick={() => toggle(day)}
            >
              {label}
            </button>
          );
        })}
      </div>
      <FieldErrorText errors={errors} field="workingDays" />
      <div className="mt-1 text-[length:var(--text-sm)] text-ink-muted">
        Used for working days left in a sprint, the days on the burndown, and the start of a new
        sprint.
      </div>
    </div>
  );
}
