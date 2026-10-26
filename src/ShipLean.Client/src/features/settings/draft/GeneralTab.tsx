import type { ProjectDto } from '../../../api/projects';
import { settingsTestIds } from '../testIds';
import { FieldErrorText } from './FieldErrorText';
import { Panel } from './Panel';
import type { TabProps } from './tabProps';
import { WorkingDays } from './WorkingDays';

const input =
  'h-7 w-full max-w-md rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink disabled:opacity-60';

export function GeneralTab({ project, ...tab }: TabProps & { project: ProjectDto }) {
  const { draft, setDraft, readOnly, errors } = tab;
  return (
    <>
      <Panel title="Project">
        <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
          Name
          <input
            id="settings-field-name"
            data-testid={settingsTestIds.generalName}
            className={input}
            value={draft.name}
            maxLength={80}
            disabled={readOnly}
            autoComplete="off"
            onChange={(e) => setDraft((d) => ({ ...d, name: e.target.value }))}
          />
          <FieldErrorText errors={errors} field="name" />
        </label>
        <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
          Description
          <textarea
            data-testid={settingsTestIds.generalDescription}
            className={`${input} h-16 max-w-lg py-1`}
            value={draft.description}
            disabled={readOnly}
            onChange={(e) => setDraft((d) => ({ ...d, description: e.target.value }))}
          />
        </label>
        <div className="grid gap-1 text-[length:var(--text-sm)]">
          <span className="font-medium text-ink">Project key</span>
          <span
            data-testid={settingsTestIds.generalKey}
            title="The project key can't be changed"
            className="font-mono font-semibold text-ink"
          >
            {project.key} <span className="font-sans font-normal text-ink-muted">Read-only</span>
          </span>
          <span className="text-ink-muted">
            Set when the project was created and never changes, so {project.key}-123 keeps pointing
            at the same item.
          </span>
        </div>
        <div className="grid gap-1 text-[length:var(--text-sm)]">
          <span className="font-medium text-ink">Next item number</span>
          <span data-testid={settingsTestIds.generalNextNumber} className="font-mono text-ink">
            {project.key}-{project.nextItemNumber}
          </span>
        </div>
      </Panel>
      <Panel title="Working days">
        <WorkingDays {...tab} />
      </Panel>
    </>
  );
}
