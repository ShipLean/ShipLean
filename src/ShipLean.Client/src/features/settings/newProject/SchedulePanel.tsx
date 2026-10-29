import { settingsTestIds as ids } from '../testIds';
import type { FormProps } from './formProps';
import { Panel } from '../draft/Panel';
import { WorkingDaysField } from './WorkingDaysField';

export function SchedulePanel({ form, patch }: FormProps) {
  return (
    <Panel title="Working days and first sprint">
      <div className="grid gap-1 text-[length:var(--text-sm)] text-ink-muted">
        Working days
        <WorkingDaysField form={form} patch={patch} />
        <span>Burndown and sprint dates skip the other days.</span>
      </div>
      <label className="flex items-center gap-2 text-[length:var(--text-sm)] text-ink">
        <input
          type="checkbox"
          data-testid={ids.newProjectSprintToggle}
          checked={form.sprint}
          onChange={(e) => patch({ sprint: e.target.checked })}
        />
        Create the first sprint
      </label>
      {form.sprint ? (
        <div className="flex items-center gap-2 text-[length:var(--text-sm)] text-ink-muted">
          <label htmlFor="settings-newproject-start">Starts</label>
          <input
            id="settings-newproject-start"
            type="date"
            data-testid={ids.newProjectSprintStart}
            className="h-7 rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink"
            value={form.start}
            onChange={(e) => patch({ datesEdited: true, start: e.target.value, end: form.end })}
          />
          <label htmlFor="settings-newproject-end">Ends</label>
          <input
            id="settings-newproject-end"
            type="date"
            data-testid={ids.newProjectSprintEnd}
            className="h-7 rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink"
            value={form.end}
            onChange={(e) => patch({ datesEdited: true, start: form.start, end: e.target.value })}
          />
        </div>
      ) : (
        <span className="text-[length:var(--text-sm)] text-ink-muted">
          The backlog works without sprints. Create one from Sprints when you are ready.
        </span>
      )}
    </Panel>
  );
}
