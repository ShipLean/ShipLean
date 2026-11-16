import { useState } from 'react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { InspectionProjectSettingsDto } from '@shiplean/core/api/inspection';
import { Panel } from '../../../settings/draft/Panel';
import { errorText } from '../../../settings/instance/model';
import { useSaveInspectionSettings } from '../../inspectionApi';
import { inspectionTestIds as ids } from '../../testIds';

const field =
  'h-[30px] w-full max-w-md rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink disabled:opacity-60';

const blankToNull = (value: string) => (value.trim() === '' ? null : value.trim());

/** The Inspection settings of one project. Project admins edit; everyone else reads. Save is this form's own (D30). */
export function GeneralForm({
  project,
  settings,
}: {
  project: ProjectDto;
  settings: InspectionProjectSettingsDto;
}) {
  const readOnly = project.role !== 'admin';
  const [enabled, setEnabled] = useState(settings.enabled);
  const [showContent, setShowContent] = useState(settings.showContent);
  const [tenant, setTenant] = useState(settings.tenantOverride ?? '');
  const [selector, setSelector] = useState(settings.selectorOverride ?? '');
  // Turning content on needs the warning confirmed; content that was already on stays confirmed.
  const [confirmed, setConfirmed] = useState(settings.showContent);
  const save = useSaveInspectionSettings(project.key);
  const locked = readOnly || save.isPending;
  const tempoLoki = settings.storeMode === 'tempoLoki';
  const needsConfirm = showContent && !confirmed;

  const submit = () => {
    save.mutate({
      enabled,
      showContent,
      tenantOverride: tempoLoki ? blankToNull(tenant) : settings.tenantOverride,
      selectorOverride: tempoLoki ? blankToNull(selector) : settings.selectorOverride,
    });
  };

  return (
    <Panel title="Inspection" testId={ids.inspectionGeneral}>
      {readOnly && (
        <p data-testid={ids.inspectionReadOnly} className="m-0 text-ink-muted">
          Only project admins can change these settings.
        </p>
      )}
      <label className="inline-flex items-center gap-2 text-[length:var(--text-sm)] font-medium text-ink">
        <input
          type="checkbox"
          data-testid={ids.inspectionEnabled}
          checked={enabled}
          disabled={locked}
          onChange={(event) => setEnabled(event.target.checked)}
        />
        Show agent runs for this project
      </label>
      <div className="grid gap-1 text-[length:var(--text-sm)]">
        <label className="inline-flex items-center gap-2 font-medium text-ink">
          <input
            type="checkbox"
            data-testid={ids.inspectionShowContent}
            checked={showContent}
            disabled={locked}
            onChange={(event) => {
              setShowContent(event.target.checked);
              if (!event.target.checked) setConfirmed(false);
            }}
          />
          Show prompts, responses and tool input and output
        </label>
        <span className="text-ink-muted">Off, runs show timing, tools and token counts only.</span>
        {showContent && (
          <div
            role="note"
            data-testid={ids.inspectionContentWarning}
            className="grid gap-2 rounded-control border border-edge bg-signal-soft p-2 text-ink"
          >
            <span>
              Command output and prompts can contain secrets such as tokens and passwords. Anyone in
              this project will see them. Redact in your collector before turning this on.
            </span>
            <label className="inline-flex items-center gap-2 font-medium">
              <input
                type="checkbox"
                data-testid={ids.inspectionContentConfirm}
                checked={confirmed}
                disabled={locked}
                onChange={(event) => setConfirmed(event.target.checked)}
              />
              I understand and want content shown
            </label>
          </div>
        )}
      </div>
      <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
        Store:{' '}
        <span data-testid={ids.inspectionStoreMode} className="text-ink">
          {tempoLoki ? 'Tempo and Loki' : 'ClickHouse'}
        </span>{' '}
        (set by the instance)
      </p>
      {tempoLoki && (
        <>
          <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
            Tenant id (optional)
            <input
              data-testid={ids.inspectionTenantOverride}
              className={field}
              value={tenant}
              disabled={locked}
              onChange={(event) => setTenant(event.target.value)}
            />
          </label>
          <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
            Extra label selector (optional)
            <input
              data-testid={ids.inspectionSelectorOverride}
              className={field}
              value={selector}
              disabled={locked}
              onChange={(event) => setSelector(event.target.value)}
            />
          </label>
        </>
      )}
      {save.isError && (
        <p role="alert" data-testid={ids.inspectionSettingsSaveError} className="m-0 text-alert">
          {errorText(save.error)}
        </p>
      )}
      {!readOnly && (
        <div>
          <button
            type="button"
            data-testid={ids.inspectionSettingsSave}
            disabled={locked || needsConfirm}
            onClick={submit}
            className="h-[30px] rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-semibold text-signal-ink disabled:opacity-60"
          >
            Save
          </button>
        </div>
      )}
    </Panel>
  );
}
