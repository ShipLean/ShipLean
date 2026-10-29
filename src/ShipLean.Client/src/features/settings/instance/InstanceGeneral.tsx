import { useState } from 'react';
import type { InstanceSettingsDto } from '../../../api/projects';
import { settingsTestIds } from '../testIds';
import { Panel } from '../draft/Panel';
import { AccessState } from './AccessState';
import { BaseUrlCheck } from './BaseUrlCheck';
import { PublicOriginNotice } from './PublicOriginNotice';
import { CreationField } from './CreationField';
import { useInstanceSettings, useSaveInstanceSettings } from './instanceApi';
import { errorText, pruneEdit, type SettingsEdit } from './model';
import { SignInRules } from './SignInRules';

const input =
  'h-7 w-full max-w-md rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink';

/** The instance admin's General tab: one draft with Save, over the saved document. */
export function InstanceGeneral() {
  const settings = useInstanceSettings();
  return (
    <AccessState pending={settings.isPending} error={settings.error}>
      {settings.data ? <GeneralForm saved={settings.data} /> : null}
    </AccessState>
  );
}

// The draft is only what the admin changed, laid over the latest saved document, so a refetch never leaves a stale copy.
function GeneralForm({ saved }: { saved: InstanceSettingsDto }) {
  const [edit, setEdit] = useState<SettingsEdit>({});
  const save = useSaveInstanceSettings();
  const draft: InstanceSettingsDto = { ...saved, ...edit };
  const dirty = Object.keys(pruneEdit(saved, edit)).length > 0;
  const change = (patch: SettingsEdit) => setEdit((e) => pruneEdit(saved, { ...e, ...patch }));

  const onSave = () =>
    save.mutate(edit, {
      onSuccess: () => setEdit({}),
    });

  return (
    <div data-testid={settingsTestIds.instanceGeneralPanel} className="p-4">
      <Panel title="General">
        <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
          Name
          <input
            data-testid={settingsTestIds.instanceName}
            className={input}
            value={draft.name}
            autoComplete="off"
            disabled={save.isPending}
            onChange={(e) => change({ name: e.target.value })}
          />
          <span className="font-normal text-ink-muted">
            Shown on the sign-in page, so people know which ShipLean they’re on.
          </span>
        </label>
        <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
          Base URL
          <input
            data-testid={settingsTestIds.instanceBaseUrl}
            className={`${input} font-mono`}
            value={draft.baseUrl}
            spellCheck={false}
            autoComplete="off"
            disabled={save.isPending}
            onChange={(e) => change({ baseUrl: e.target.value })}
          />
          <BaseUrlCheck baseUrl={draft.baseUrl} origin={window.location.origin} />
          <PublicOriginNotice baseUrl={saved.baseUrl} />
          <span className="font-normal text-ink-muted">
            For links ShipLean builds outside the browser: copied work item links, the sign-in link
            you hand to new users, and links back from Jira import.
          </span>
        </label>
        <CreationField
          value={draft.projectCreation}
          disabled={save.isPending}
          onChange={(projectCreation) => change({ projectCreation })}
        />
        {save.isError ? (
          <p
            role="alert"
            data-testid={settingsTestIds.instanceSaveError}
            className="m-0 text-[length:var(--text-sm)] text-alert"
          >
            {errorText(save.error)}
          </p>
        ) : null}
        <div className="flex items-center gap-2 text-[length:var(--text-sm)]">
          <span data-testid={settingsTestIds.instanceDirty} className="text-ink-muted">
            {dirty ? 'Unsaved changes' : 'Saved'}
          </span>
          <span className="flex-1" />
          <button
            type="button"
            data-testid={settingsTestIds.instanceDiscard}
            className="btn-ghost"
            disabled={!dirty}
            onClick={() => {
              save.reset();
              setEdit({});
            }}
          >
            Discard
          </button>
          <button
            type="button"
            data-testid={settingsTestIds.instanceSave}
            className="btn-signal"
            disabled={!dirty || save.isPending}
            onClick={onSave}
          >
            Save
          </button>
        </div>
      </Panel>
      <SignInRules />
    </div>
  );
}
