import type { KeyCheckDto, ProjectSummaryDto } from '../../../api/projects';
import { Panel } from '../draft/Panel';
import { settingsTestIds as ids } from '../testIds';
import type { FormProps } from './formProps';
import { KeyField } from './KeyField';

interface ProjectPanelProps extends FormProps {
  check: KeyCheckDto | undefined;
  pending: boolean;
  sources: ProjectSummaryDto[];
  /** The key in force: the typed or suggested one, or the alternative that replaced a taken suggestion. */
  shownKey: string;
  skippedFrom: string | undefined;
}

export function ProjectPanel({
  form,
  patch,
  check,
  pending,
  sources,
  shownKey,
  skippedFrom,
}: ProjectPanelProps) {
  return (
    <Panel title="Project">
      <label className="grid gap-1 text-[length:var(--text-sm)] text-ink-muted">
        Name
        <input
          data-testid={ids.newProjectName}
          autoComplete="off"
          className="h-7 rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink w-80"
          value={form.name}
          onChange={(e) => patch({ name: e.target.value })}
        />
      </label>
      <div className="grid gap-1 text-[length:var(--text-sm)] text-ink-muted">
        Key
        <KeyField
          form={form}
          patch={patch}
          check={check}
          pending={pending}
          sources={sources}
          shownKey={shownKey}
          skippedFrom={skippedFrom}
        />
      </div>
    </Panel>
  );
}
