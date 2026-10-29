import type { AgentConnectionOption, ProjectDto } from '../../../api/projects';
import { settingsTestIds } from '../testIds';
import { HolidaysPanel } from './HolidaysPanel';
import { FieldErrorText } from './FieldErrorText';
import { Panel } from './Panel';
import type { TabProps } from './tabProps';
import { WorkingDays } from './WorkingDays';

const input =
  'h-7 w-full max-w-md rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink disabled:opacity-60';

export function GeneralTab({
  project,
  agentConnections,
  ...tab
}: TabProps & { project: ProjectDto; agentConnections: AgentConnectionOption[] }) {
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
      <Panel title="Agent">
        <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
          Agent connection
          <select
            data-testid={settingsTestIds.generalAgentConnection}
            className={input}
            value={draft.agentConnectionId}
            disabled={readOnly}
            onChange={(e) => setDraft((d) => ({ ...d, agentConnectionId: e.target.value }))}
          >
            <option value="">None</option>
            {agentConnections.map((c) => (
              <option key={c.id} value={c.id} disabled={c.archived}>
                {c.archived ? `${c.name} (archived)` : c.name}
              </option>
            ))}
          </select>
          <FieldErrorText errors={errors} field="agentConnectionId" />
          <span className="font-normal text-ink-muted">
            Where items sent to an agent go. An instance admin adds connections in Instance
            settings.
          </span>
        </label>
        <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
          Repository URL
          <input
            data-testid={settingsTestIds.generalRepoUrl}
            className={`${input} font-mono`}
            value={draft.repoUrl}
            placeholder="https://github.com/org/repo.git"
            spellCheck={false}
            autoComplete="off"
            disabled={readOnly}
            onChange={(e) => setDraft((d) => ({ ...d, repoUrl: e.target.value }))}
          />
          <FieldErrorText errors={errors} field="repoUrl" />
        </label>
        <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
          Base branch
          <input
            data-testid={settingsTestIds.generalRepoBaseBranch}
            className={`${input} font-mono`}
            value={draft.repoBaseBranch}
            placeholder="main"
            spellCheck={false}
            autoComplete="off"
            disabled={readOnly}
            onChange={(e) => setDraft((d) => ({ ...d, repoBaseBranch: e.target.value }))}
          />
          <FieldErrorText errors={errors} field="repoBaseBranch" />
        </label>
      </Panel>
      <Panel title="Working days">
        <WorkingDays {...tab} />
      </Panel>
      <HolidaysPanel {...tab} />
    </>
  );
}
