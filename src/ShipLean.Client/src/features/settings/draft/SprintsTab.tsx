import { Link } from 'react-router-dom';
import type { ProjectDto } from '../../../api/projects';
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
            Change its dates in All sprints for a one-off.
          </p>
        </div>
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
