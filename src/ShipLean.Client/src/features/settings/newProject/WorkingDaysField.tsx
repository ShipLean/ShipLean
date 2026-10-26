import { settingsTestIds as ids } from '../testIds';
import type { FormProps } from './formProps';

const DAYS = [
  ['Mon', ids.workingDay1],
  ['Tue', ids.workingDay2],
  ['Wed', ids.workingDay3],
  ['Thu', ids.workingDay4],
  ['Fri', ids.workingDay5],
  ['Sat', ids.workingDay6],
  ['Sun', ids.workingDay7],
] as const;

export function WorkingDaysField({ form, patch }: FormProps) {
  const toggle = (day: number) =>
    patch({
      workingDays: form.workingDays.includes(day)
        ? form.workingDays.filter((d) => d !== day)
        : [...form.workingDays, day].sort((a, b) => a - b),
    });
  return (
    <div
      role="group"
      aria-label="Working days"
      data-testid={ids.workingDays}
      className="flex gap-1"
    >
      {DAYS.map(([label, testId], index) => {
        const day = index + 1;
        const on = form.workingDays.includes(day);
        return (
          <button
            key={day}
            type="button"
            data-testid={testId}
            aria-pressed={on}
            className={`h-7 w-11 rounded-control border text-[length:var(--text-sm)] font-medium ${
              on ? 'border-signal bg-signal-soft text-ink' : 'border-edge bg-panel text-ink-muted'
            }`}
            onClick={() => toggle(day)}
          >
            {label}
          </button>
        );
      })}
    </div>
  );
}
