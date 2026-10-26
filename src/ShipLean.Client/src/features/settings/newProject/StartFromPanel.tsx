import type { ProjectSummaryDto } from '../../../api/projects';
import { Panel } from '../draft/Panel';
import { settingsTestIds as ids } from '../testIds';
import type { FormProps } from './formProps';

interface StartFromPanelProps extends FormProps {
  sources: ProjectSummaryDto[];
}

export function StartFromPanel({ form, patch, sources }: StartFromPanelProps) {
  const copyFirst = () =>
    patch({ from: 'copy', copyFromId: form.copyFromId ?? sources[0]?.id ?? null });
  return (
    <Panel title="Start from">
      <label className="grid gap-0.5 text-[length:var(--text-sm)] text-ink">
        <span className="flex items-center gap-2 font-medium">
          <input
            type="radio"
            name="settings-newproject-from"
            data-testid={ids.newProjectFromDefaults}
            checked={form.from === 'defaults'}
            onChange={() => patch({ from: 'defaults' })}
          />
          ShipLean defaults
        </span>
        <span className="pl-6 text-ink-muted">
          New, Active, Resolved, Closed and Removed states for every type, the default board
          columns.
        </span>
      </label>
      <label className="grid gap-0.5 text-[length:var(--text-sm)] text-ink">
        <span className="flex items-center gap-2 font-medium">
          <input
            type="radio"
            name="settings-newproject-from"
            data-testid={ids.newProjectFromCopy}
            disabled={sources.length === 0}
            checked={form.from === 'copy'}
            onChange={copyFirst}
          />
          Copy settings from a project you are in
        </span>
        <span className="pl-6 text-ink-muted">
          States and board settings. Never copied: work items, sprints, members, the Jira
          connection.
        </span>
      </label>
      {form.from === 'copy' ? (
        <select
          data-testid={ids.newProjectCopySource}
          aria-label="Project to copy from"
          className="h-7 rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink w-64"
          value={form.copyFromId ?? ''}
          onChange={(e) => patch({ copyFromId: e.target.value })}
        >
          {sources.map((p) => (
            <option key={p.id} value={p.id}>
              {p.key} · {p.name}
            </option>
          ))}
        </select>
      ) : null}
    </Panel>
  );
}
