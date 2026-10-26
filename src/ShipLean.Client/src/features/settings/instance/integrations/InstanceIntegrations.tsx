import { useState } from 'react';
import { settingsTestIds } from '../../testIds';
import { Panel } from '../../draft/Panel';
import { AccessState } from '../AccessState';
import { errorText } from '../model';
import { AllowListField } from './AllowListField';
import { FieldError } from './FieldError';
import { useIntegrations, useSaveIntegrations } from './integrationsApi';
import {
  allowTextOf,
  errorField,
  gitLabUrlChanged,
  gitLabUrlOf,
  isDirty,
  noEdit,
  toSaveDto,
  type IntegrationsEdit,
  type IntegrationsSettingsDto,
} from './model';
import { TokenField, type TokenFieldIds } from './TokenField';

const input =
  'h-7 w-full max-w-md rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink disabled:opacity-60';

const gitHubIds: TokenFieldIds = {
  input: settingsTestIds.integrationsGitHubTokenInput,
  lastFour: settingsTestIds.integrationsGitHubTokenLastFour,
  replace: settingsTestIds.integrationsGitHubTokenReplace,
  clear: settingsTestIds.integrationsGitHubTokenClear,
  env: settingsTestIds.integrationsGitHubTokenEnv,
};
const gitLabIds: TokenFieldIds = {
  input: settingsTestIds.integrationsGitLabTokenInput,
  lastFour: settingsTestIds.integrationsGitLabTokenLastFour,
  replace: settingsTestIds.integrationsGitLabTokenReplace,
  clear: settingsTestIds.integrationsGitLabTokenClear,
  env: settingsTestIds.integrationsGitLabEnv,
};

/** The instance admin's Integrations tab: GitHub and GitLab for link cards, and the preview allow-list. */
export function InstanceIntegrations() {
  const integrations = useIntegrations();
  return (
    <AccessState pending={integrations.isPending} error={integrations.error}>
      {integrations.data ? <IntegrationsForm saved={integrations.data} /> : null}
    </AccessState>
  );
}

// One draft with Save over the saved document. A failed save keeps the draft.
function IntegrationsForm({ saved }: { saved: IntegrationsSettingsDto }) {
  const [edit, setEdit] = useState<IntegrationsEdit>(noEdit);
  const save = useSaveIntegrations();
  const dirty = isDirty(saved, edit);
  const locked = save.isPending;
  const blamed = errorField(save.error);
  const message = save.isError ? errorText(save.error) : undefined;
  const fieldMessage = (field: string) => (blamed === field ? message : undefined);
  const named = ['gitHubToken', 'gitLabUrl', 'gitLabToken', 'previewAllowHosts'].includes(
    blamed ?? '',
  );
  // A new address leaves the saved GitLab token behind, so the token field starts empty.
  const urlChanged = gitLabUrlChanged(saved, edit);

  return (
    <div data-testid={settingsTestIds.integrationsPanel} className="p-4">
      <Panel title="GitHub">
        <TokenField
          label="Access token"
          hint="Optional. Lets link cards show the state of pull requests and issues in private repositories."
          lastFour={saved.gitHubTokenLastFour}
          fromEnv={saved.gitHubTokenFromEnv}
          edit={edit.gitHub}
          ids={gitHubIds}
          disabled={locked}
          error={fieldMessage('gitHubToken')}
          onChange={(gitHub) => setEdit((e) => ({ ...e, gitHub }))}
        />
      </Panel>
      <Panel title="GitLab">
        <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
          URL
          <input
            data-testid={settingsTestIds.integrationsGitLabUrl}
            className={`${input} font-mono`}
            value={gitLabUrlOf(saved, edit)}
            spellCheck={false}
            autoComplete="off"
            disabled={locked}
            onChange={(e) => setEdit((s) => ({ ...s, gitLabUrl: e.target.value }))}
          />
          <span className="font-normal text-ink-muted">
            Optional, such as https://gitlab.example.com. A token is kept with the address it was
            saved for, so a new address removes the saved token.
          </span>
          <FieldError message={fieldMessage('gitLabUrl')} />
        </label>
        <TokenField
          label="Access token"
          hint="Optional. Needs read access to the projects whose merge requests and issues are linked."
          lastFour={urlChanged ? null : saved.gitLabTokenLastFour}
          fromEnv={!urlChanged && saved.gitLabFromEnv}
          edit={edit.gitLab}
          ids={gitLabIds}
          disabled={locked}
          error={fieldMessage('gitLabToken')}
          onChange={(gitLab) => setEdit((e) => ({ ...e, gitLab }))}
        />
      </Panel>
      <Panel title="Link previews">
        <AllowListField
          text={allowTextOf(saved, edit)}
          disabled={locked}
          error={fieldMessage('previewAllowHosts')}
          onChange={(allowText) => setEdit((e) => ({ ...e, allowText }))}
        />
      </Panel>
      {message !== undefined && !named ? (
        <p
          role="alert"
          data-testid={settingsTestIds.integrationsSaveError}
          className="m-0 mb-3 text-[length:var(--text-sm)] text-alert"
        >
          {message}
        </p>
      ) : null}
      <div className="flex items-center gap-2 text-[length:var(--text-sm)]">
        <span data-testid={settingsTestIds.integrationsDirty} className="text-ink-muted">
          {dirty ? 'Unsaved changes' : 'Saved'}
        </span>
        <span className="flex-1" />
        <button
          type="button"
          data-testid={settingsTestIds.integrationsDiscard}
          className="btn-ghost"
          disabled={!dirty || locked}
          onClick={() => {
            save.reset();
            setEdit(noEdit);
          }}
        >
          Discard
        </button>
        <button
          type="button"
          data-testid={settingsTestIds.integrationsSave}
          className="btn-signal"
          disabled={!dirty || locked}
          onClick={() => save.mutate(toSaveDto(saved, edit), { onSuccess: () => setEdit(noEdit) })}
        >
          Save
        </button>
      </div>
    </div>
  );
}
