import { useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiRequestError } from '../../../api/client';
import type { ProjectDto } from '../../../api/projects';
import { routePath } from '../../../routes';
import { useProjectMembers } from '../../workItem/workItemApi';
import { Panel } from '../draft/Panel';
import { settingsTestIds as ids } from '../testIds';
import { fetchIssueIntake, issueIntakeKey, saveIssueIntake } from './api';
import type { IssueIntakeSettingsDto } from './types';

const field =
  'h-7 w-full max-w-md rounded-control border border-edge bg-panel px-2 text-[length:var(--text-sm)] text-ink disabled:opacity-60';

/** Project settings, GitHub issues: loads the intake settings and shows the form once they arrive. */
export function IssueIntakeSection({ project }: { project: ProjectDto }) {
  const query = useQuery({
    queryKey: issueIntakeKey(project.id),
    queryFn: () => fetchIssueIntake(project.id),
  });
  if (query.data === undefined) {
    return (
      <Panel title="GitHub issues" testId={ids.issueIntakePanel}>
        <p className="text-[length:var(--text-sm)] text-ink-muted">
          {query.isError ? query.error.message : 'Loading the GitHub issues settings'}
        </p>
      </Panel>
    );
  }
  return (
    <IssueIntakeForm project={project} settings={query.data} readOnly={project.role !== 'admin'} />
  );
}

function IssueIntakeForm({
  project,
  settings,
  readOnly,
}: {
  project: ProjectDto;
  settings: IssueIntakeSettingsDto;
  readOnly: boolean;
}) {
  const queryClient = useQueryClient();
  const members = useProjectMembers(project.id).data ?? [];
  const [enabled, setEnabled] = useState(settings.enabled);
  const [runAsUserId, setRunAsUserId] = useState(settings.runAs?.id ?? '');
  const [text, setText] = useState(
    settings.proposalInstructions || settings.defaultProposalInstructions,
  );
  const [maxRounds, setMaxRounds] = useState(String(settings.maxRounds));
  const save = useMutation({
    mutationFn: () =>
      saveIssueIntake(project.id, {
        enabled,
        runAsUserId: runAsUserId === '' ? null : runAsUserId,
        // The default text is stored as empty so a later change to the default reaches this project.
        proposalInstructions: text.trim() === settings.defaultProposalInstructions ? '' : text,
        maxRounds: Number(maxRounds),
      }),
    onSuccess: (saved) => queryClient.setQueryData(issueIntakeKey(project.id), saved),
  });
  const locked = readOnly || save.isPending;
  const failure = save.error instanceof ApiRequestError ? save.error : undefined;
  const failedField = failure === undefined ? undefined : errorField(failure);
  const errorFor = (name: string): ReactNode =>
    failure !== undefined && failedField === name ? (
      <div role="alert" data-testid={ids.issueIntakeError} data-field={name} className="text-alert">
        {failure.message}
      </div>
    ) : null;
  const shownBesideField =
    failedField !== undefined &&
    ['enabled', 'runAsUserId', 'repoUrl', 'proposalInstructions', 'maxRounds'].includes(
      failedField,
    );
  const other = save.error !== null && !shownBesideField ? save.error.message : undefined;

  const readiness = settings.readiness;
  const missing = [
    readiness.hasGitHubRepository
      ? undefined
      : {
          id: 'repository',
          text: 'The project has no github.com repository.',
          fix: 'general' as const,
        },
    readiness.hasAgentConnection
      ? undefined
      : { id: 'agent', text: 'The project has no agent connection.', fix: 'general' as const },
    readiness.hasGitHubToken
      ? undefined
      : {
          id: 'token',
          text: 'No GitHub token. An instance admin sets one with permission to read issues and write comments on the repository.',
        },
    readiness.hasWebhookSecret
      ? undefined
      : {
          id: 'secret',
          text: 'No webhook secret. An instance admin sets one, then adds the same secret to the repository webhook.',
        },
    readiness.hasRunAs
      ? undefined
      : {
          id: 'run-as',
          text: 'No run-as member. Choose one below; intake cannot be turned on without one.',
        },
  ].filter((item) => item !== undefined);

  return (
    <Panel title="GitHub issues" testId={ids.issueIntakePanel}>
      <p className="text-[length:var(--text-sm)] text-ink-muted">
        A new issue on the repository becomes a Triage item, and an agent proposes a rewrite for the
        issue author to approve with LGTM.
      </p>
      <div className="grid gap-1 text-[length:var(--text-sm)]">
        <span className="font-medium text-ink">Repository</span>
        <span data-testid={ids.issueIntakeRepository} className="font-mono text-ink">
          {settings.repository ?? 'None'}{' '}
          <span className="font-sans font-normal text-ink-muted">Read-only</span>
        </span>
        <span className="text-ink-muted">Taken from the Repository URL in General settings.</span>
        {errorFor('repoUrl')}
      </div>
      <div
        data-testid={ids.issueIntakeReadiness}
        className="grid gap-1 text-[length:var(--text-sm)]"
      >
        <span className="font-medium text-ink">Ready to turn on</span>
        {missing.length === 0 ? (
          <span className="text-ink-muted">Everything is in place.</span>
        ) : (
          <ul className="grid gap-1">
            {missing.map((item) => (
              <li
                key={item.id}
                data-testid={ids.issueIntakeMissing}
                data-item={item.id}
                className="text-alert"
              >
                {item.text}{' '}
                {item.fix === undefined ? null : (
                  <Link
                    className="text-signal"
                    to={routePath('projectSettings', { key: project.key, tab: item.fix })}
                  >
                    Open General settings
                  </Link>
                )}
              </li>
            ))}
          </ul>
        )}
        <span className="text-ink-muted">
          The agent relay must also allow this repository. Whoever runs the relay adds it to the
          relay&apos;s allow-list; ShipLean cannot check that.
        </span>
      </div>
      <div className="grid gap-1 text-[length:var(--text-sm)]">
        <label className="inline-flex items-center gap-2 font-medium text-ink">
          <input
            type="checkbox"
            data-testid={ids.issueIntakeEnabled}
            checked={enabled}
            disabled={locked}
            onChange={(e) => setEnabled(e.target.checked)}
          />
          Turn GitHub issues into Triage items
        </label>
        {errorFor('enabled')}
      </div>
      <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
        Run as
        <select
          data-testid={ids.issueIntakeRunAs}
          className={field}
          value={runAsUserId}
          disabled={locked}
          onChange={(e) => setRunAsUserId(e.target.value)}
        >
          <option value="">None</option>
          {members.map((m) => (
            <option key={m.user.id} value={m.user.id}>
              {m.user.name}
            </option>
          ))}
        </select>
        <span className="font-normal text-ink-muted">
          The member the agent works as. Every comment ShipLean writes about an issue is authored by
          this person.
        </span>
        {errorFor('runAsUserId')}
      </label>
      <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
        Proposal instructions
        <textarea
          data-testid={ids.issueIntakeInstructions}
          className={`${field} h-28 max-w-2xl py-1`}
          value={text}
          disabled={locked}
          onChange={(e) => setText(e.target.value)}
        />
        {errorFor('proposalInstructions')}
      </label>
      <div>
        <button
          type="button"
          data-testid={ids.issueIntakeReset}
          className="btn-ghost"
          disabled={locked || text === settings.defaultProposalInstructions}
          onClick={() => setText(settings.defaultProposalInstructions)}
        >
          Reset to default
        </button>
      </div>
      <label className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
        Max rounds
        <input
          type="number"
          min={1}
          data-testid={ids.issueIntakeMaxRounds}
          className={`${field} max-w-[8rem]`}
          value={maxRounds}
          disabled={locked}
          onChange={(e) => setMaxRounds(e.target.value)}
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
            disabled={locked}
            onClick={() => save.mutate()}
          >
            Save
          </button>
        </div>
      )}
    </Panel>
  );
}

// The field a refusal names; the error body carries it (ApiRequestError.body.field).
function errorField(error: ApiRequestError): string | undefined {
  const body = error.body as { field?: unknown } | undefined;
  return typeof body?.field === 'string' ? body.field : undefined;
}
