import { Link } from 'react-router-dom';
import type { ProjectDto, SprintNaming } from '../../../api/projects';
import { routePath } from '../../../routes';
import { settingsTestIds } from '../testIds';
import { FieldErrorText } from './FieldErrorText';
import { Panel } from './Panel';
import type { TabProps } from './tabProps';

const input =
  'h-7 w-20 rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink disabled:opacity-60';

export function SprintsTab({
  project,
  draft,
  setDraft,
  readOnly,
  errors,
}: TabProps & { project: ProjectDto }) {
  return (
    <>
      <Panel title="Sprints">
        <div className="text-[length:var(--text-sm)]" data-testid={settingsTestIds.sprintLength}>
          <span className="font-medium text-ink">Sprint length</span>
          <p className="text-ink-muted">
            A new sprint starts on the next working day after the last one and gets the same length.
            To change one sprint's dates, open All sprints and press Edit on its row, or use Edit
            sprint in its header.
          </p>
        </div>
        <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
          Sprint naming
          <select
            id="settings-field-sprintNaming"
            data-testid={settingsTestIds.sprintNaming}
            className="h-7 w-fit rounded-control border border-edge bg-panel px-1 text-[length:var(--text-sm)] text-ink disabled:opacity-60"
            value={draft.sprintNaming}
            disabled={readOnly}
            onChange={(e) => {
              const sprintNaming: SprintNaming =
                e.target.value === 'yearQuarterNumber' ? 'yearQuarterNumber' : 'number';
              setDraft((d) => ({ ...d, sprintNaming }));
            }}
          >
            <option value="yearQuarterNumber">Year, quarter and number — 2026 Q4 Sprint 3</option>
            <option value="number">Number only — Sprint 129</option>
          </select>
          <span
            className="font-normal text-ink-muted"
            data-testid={settingsTestIds.sprintNamingHint}
          >
            Existing sprints keep their names; this applies to the next sprint you create.
          </span>
        </label>
        <Link
          to={routePath('sprints', { key: project.key })}
          className="text-[length:var(--text-sm)] text-signal"
        >
          All sprints
        </Link>
      </Panel>
      <Panel title="Velocity and forecast" testId={settingsTestIds.sprintsPanel}>
        <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
          Velocity window (ended sprints averaged)
          <input
            id="settings-field-velocityWindow"
            data-testid={settingsTestIds.velocityWindow}
            className={input}
            value={draft.velocityWindow}
            inputMode="numeric"
            disabled={readOnly}
            autoComplete="off"
            onChange={(e) => setDraft((d) => ({ ...d, velocityWindow: e.target.value }))}
          />
          <FieldErrorText errors={errors} field="velocityWindow" />
        </label>
        <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
          Velocity override (points)
          <span className="flex items-center gap-2">
            <input
              id="settings-field-velocityOverride"
              data-testid={settingsTestIds.velocityOverride}
              className={input}
              value={draft.velocityOverride}
              inputMode="decimal"
              disabled={readOnly}
              autoComplete="off"
              onChange={(e) => setDraft((d) => ({ ...d, velocityOverride: e.target.value }))}
            />
            {draft.velocityOverride === '' || readOnly ? null : (
              <button
                type="button"
                data-testid={settingsTestIds.velocityClear}
                className="text-[length:var(--text-sm)] text-signal"
                onClick={() => setDraft((d) => ({ ...d, velocityOverride: '' }))}
              >
                Use calculated
              </button>
            )}
          </span>
          <FieldErrorText errors={errors} field="velocityOverride" />
          <span className="font-normal text-ink-muted">
            Leave it blank to use the calculated velocity.
          </span>
        </label>
      </Panel>
    </>
  );
}
