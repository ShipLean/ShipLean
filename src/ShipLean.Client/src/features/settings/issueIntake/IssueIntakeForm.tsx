import { useEffect, useId, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ApiRequestError } from '@shiplean/core/api/client';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { useProjectMembers } from '../../workItem/workItemApi';
import { Panel } from '../draft/Panel';
import { settingsTestIds as ids } from '../testIds';
import { issueIntakeKey, saveIssueIntake } from './api';
import { errorField, FieldError, fieldErrorId } from './FieldError';
import { IssueIntakeReadiness } from './IssueIntakeReadiness';
import type { IssueIntakeSettingsDto } from './types';

const field =
  'h-[var(--control-height)] w-full max-w-md rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink disabled:opacity-60';

// The fields a save refusal can name, in page order, with the one that takes focus.
const fieldOrder = ['enabled', 'runAsUserId', 'proposalInstructions', 'maxRounds'] as const;
type FieldName = (typeof fieldOrder)[number];

const controlId = (scope: string, name: FieldName) => `${scope}-control-${name}`;

/** The editable GitHub issues settings of one project, with their own Save. */
export function IssueIntakeForm({
  project,
  settings,
  readOnly,
}: {
  project: ProjectDto;
  settings: IssueIntakeSettingsDto;
  readOnly: boolean;
}) {
  const queryClient = useQueryClient();
  const scope = useId();
  const membersQuery = useProjectMembers(project.id);
  const [enabled, setEnabled] = useState(settings.enabled);
  const [runAsUserId, setRunAsUserId] = useState(settings.runAs?.id ?? '');
  const [text, setText] = useState(
    settings.proposalInstructions || settings.defaultProposalInstructions,
  );
  // Follow the default: set when stored empty or after Reset, cleared by any edit of the text.
  const [useDefault, setUseDefault] = useState(settings.proposalInstructions === '');
  const [maxRounds, setMaxRounds] = useState(String(settings.maxRounds));
  const save = useMutation({
    mutationFn: () =>
      saveIssueIntake(project.id, {
        enabled,
        runAsUserId: runAsUserId === '' ? null : runAsUserId,
        proposalInstructions: useDefault ? '' : text,
        maxRounds: Number(maxRounds),
      }),
    onSuccess: (saved) => queryClient.setQueryData(issueIntakeKey(project.id), saved),
  });
  const failure = save.error instanceof ApiRequestError ? save.error : undefined;
  const named = failure === undefined ? undefined : errorField(failure);
  const failedField = fieldOrder.find((name) => name === named);
  useEffect(() => {
    if (failedField !== undefined) document.getElementById(controlId(scope, failedField))?.focus();
  }, [save.error, failedField, scope]);

  const locked = readOnly || save.isPending;
  const membersReady = membersQuery.data !== undefined;
  const members = membersQuery.data ?? (settings.runAs === null ? [] : [{ user: settings.runAs }]);
  const mark = (name: FieldName) => ({
    id: controlId(scope, name),
    'aria-invalid': failedField === name || undefined,
    'aria-describedby': failedField === name ? fieldErrorId(scope, name) : undefined,
  });
  const errorFor = (name: string) =>
    failure !== undefined && named === name ? (
      <FieldError id={fieldErrorId(scope, name)} field={name} message={failure.message} />
    ) : null;
  const other =
    save.error !== null && failedField === undefined && named !== 'repoUrl'
      ? save.error.message
      : undefined;

  return (
    <Panel title="GitHub issues" testId={ids.issueIntakePanel}>
      <p className="text-[length:var(--text-sm)] text-ink-muted">
        A new issue on the repository becomes a Triage item, and an agent proposes a rewrite for the
        issue author to approve with LGTM.
      </p>
      <div className="grid gap-2 text-[length:var(--text-sm)]">
        <span className="font-medium text-ink">Repository</span>
        <span data-testid={ids.issueIntakeRepository} className="font-mono text-ink">
          {settings.repository ?? 'None'}{' '}
          <span className="font-sans font-normal text-ink-muted">Read-only</span>
        </span>
        <span className="text-ink-muted">Taken from the Repository URL in General settings.</span>
        {errorFor('repoUrl')}
      </div>
      <IssueIntakeReadiness project={project} readiness={settings.readiness} />
      <div className="grid gap-2 text-[length:var(--text-sm)]">
        <label className="inline-flex items-center gap-2 font-medium text-ink">
          <input
            type="checkbox"
            data-testid={ids.issueIntakeEnabled}
            checked={enabled}
            disabled={locked}
            onChange={(e) => setEnabled(e.target.checked)}
            {...mark('enabled')}
          />
          Turn GitHub issues into Triage items
        </label>
        {errorFor('enabled')}
      </div>
      <div className="grid gap-2 text-[length:var(--text-sm)]">
        <label className="grid gap-2 font-medium text-ink">
          Run as
          <select
            data-testid={ids.issueIntakeRunAs}
            className={field}
            value={runAsUserId}
            disabled={locked || !membersReady}
            onChange={(e) => setRunAsUserId(e.target.value)}
            {...mark('runAsUserId')}
          >
            <option value="">None</option>
            {members.map((m) => (
              <option key={m.user.id} value={m.user.id}>
                {m.user.name}
              </option>
            ))}
          </select>
        </label>
        {membersQuery.isError ? (
          <div role="alert" data-testid={ids.issueIntakeMembersError} className="text-alert">
            Could not load the project members.{' '}
            <button
              type="button"
              data-testid={ids.issueIntakeMembersRetry}
              className="btn-ghost"
              onClick={() => void membersQuery.refetch()}
            >
              Retry
            </button>
          </div>
        ) : membersReady ? null : (
          <span data-testid={ids.issueIntakeMembersLoading} className="text-ink-muted">
            Loading members
          </span>
        )}
        <span className="text-ink-muted">
          The member the agent works as. Every comment ShipLean writes about an issue is authored by
          this person.
        </span>
        {errorFor('runAsUserId')}
      </div>
      <label className="grid gap-2 text-[length:var(--text-sm)] font-medium text-ink">
        Proposal instructions
        <textarea
          data-testid={ids.issueIntakeInstructions}
          className={`${field} h-28 max-w-2xl py-1`}
          value={text}
          disabled={locked}
          onChange={(e) => {
            setText(e.target.value);
            setUseDefault(false);
          }}
          {...mark('proposalInstructions')}
        />
        {errorFor('proposalInstructions')}
      </label>
      <div>
        <button
          type="button"
          data-testid={ids.issueIntakeReset}
          className="btn-ghost"
          disabled={locked || useDefault}
          onClick={() => {
            setText(settings.defaultProposalInstructions);
            setUseDefault(true);
          }}
        >
          Reset to default
        </button>
      </div>
      <label className="grid gap-2 text-[length:var(--text-sm)] font-medium text-ink">
        Max rounds
        <input
          type="number"
          min={1}
          data-testid={ids.issueIntakeMaxRounds}
          className={`${field} max-w-[8rem]`}
          value={maxRounds}
          disabled={locked}
          onChange={(e) => setMaxRounds(e.target.value)}
          {...mark('maxRounds')}
        />
        <span className="font-normal text-ink-muted">
          How many times the agent may rewrite a proposal after feedback.
        </span>
        {errorFor('maxRounds')}
      </label>
      {other === undefined ? null : (
        <div
          role="alert"
          data-testid={ids.issueIntakeError}
          className="text-[length:var(--text-sm)] text-alert"
        >
          {other}
        </div>
      )}
      {readOnly ? (
        <p className="text-[length:var(--text-sm)] text-ink-muted">
          Only project admins change these settings.
        </p>
      ) : (
        <div>
          <button
            type="button"
            data-testid={ids.issueIntakeSave}
            className="btn-signal"
            disabled={locked || !membersReady}
            onClick={() => save.mutate()}
          >
            Save
          </button>
        </div>
      )}
    </Panel>
  );
}
