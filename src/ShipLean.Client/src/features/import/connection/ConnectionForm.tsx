import { useState } from 'react';
import type { JiraConnectionDto, JiraKind, JiraTestResultDto } from '../../../api/import';
import { ApiRequestError } from '../../../api/client';
import { useSaveConnection, useTestConnection } from '../importApi';
import { importTestIds as ids } from '../testIds';
import { ConnectionResult } from './ConnectionResult';

const kinds: { kind: JiraKind; label: string; testId: string }[] = [
  { kind: 'cloud', label: 'Jira Cloud', testId: ids.kindCloud },
  { kind: 'server', label: 'Jira Server / Data Center', testId: ids.kindServer },
];

export function ConnectionForm({
  projectId,
  saved,
  onTested,
}: {
  projectId: string;
  saved: JiraConnectionDto | null;
  onTested?: (result: JiraTestResultDto | null) => void;
}) {
  const [replacing, setReplacing] = useState(false);
  const [kind, setKind] = useState<JiraKind>(saved?.kind ?? 'cloud');
  const [baseUrl, setBaseUrl] = useState(saved?.baseUrl ?? '');
  const [email, setEmail] = useState(saved?.email ?? '');
  const [token, setToken] = useState('');
  const [result, setResult] = useState<JiraTestResultDto | null>(null);
  const save = useSaveConnection(projectId);
  const test = useTestConnection(projectId);
  const editing = saved === null || replacing;
  const canSave =
    baseUrl.trim() !== '' && token.trim() !== '' && (kind === 'server' || email.trim() !== '');

  const onSave = () => {
    save.mutate(
      { baseUrl: baseUrl.trim(), kind, email: kind === 'cloud' ? email.trim() : '', token },
      {
        onSuccess: () => {
          setToken('');
          save.reset();
          setReplacing(false);
          setResult(null);
          onTested?.(null);
        },
      },
    );
  };
  const onTest = () => {
    test.mutate(undefined, {
      onSuccess: (answer) => {
        setResult(answer);
        onTested?.(answer);
      },
    });
  };

  return (
    <div data-testid={ids.connectionPanel} className="flex flex-col gap-[var(--space-3)]">
      <div role="group" aria-label="Deployment" className="flex gap-[var(--space-2)]">
        {kinds.map((option) => (
          <button
            key={option.kind}
            type="button"
            data-testid={option.testId}
            aria-pressed={kind === option.kind}
            disabled={!editing}
            className={kind === option.kind ? 'btn-signal' : 'btn-ghost'}
            onClick={() => setKind(option.kind)}
          >
            {option.label}
          </button>
        ))}
      </div>
      <label className="flex flex-col gap-1 text-[length:var(--text-sm)]">
        Base URL
        <input
          data-testid={ids.baseUrl}
          className="field"
          value={baseUrl}
          disabled={!editing}
          placeholder="https://your-site.atlassian.net"
          onChange={(event) => setBaseUrl(event.target.value)}
        />
      </label>
      {kind === 'cloud' ? (
        <label className="flex flex-col gap-1 text-[length:var(--text-sm)]">
          Email
          <input
            data-testid={ids.email}
            className="field"
            value={email}
            disabled={!editing}
            placeholder="you@company.com"
            onChange={(event) => setEmail(event.target.value)}
          />
        </label>
      ) : null}
      {editing ? (
        <label className="flex flex-col gap-1 text-[length:var(--text-sm)]">
          {kind === 'cloud' ? 'API token' : 'Personal access token'}
          <input
            data-testid={ids.token}
            className="field"
            type="password"
            autoComplete="new-password"
            value={token}
            placeholder={kind === 'cloud' ? 'Paste an API token' : 'Paste a personal access token'}
            onChange={(event) => setToken(event.target.value)}
          />
        </label>
      ) : (
        <div className="flex items-center gap-[var(--space-2)] text-[length:var(--text-sm)]">
          <span data-testid={ids.tokenSaved}>
            Token saved{saved.tokenLastFour === '' ? '' : `, ends ${saved.tokenLastFour}`}
          </span>
          <button
            type="button"
            data-testid={ids.replace}
            className="btn-ghost"
            onClick={() => setReplacing(true)}
          >
            Replace
          </button>
        </div>
      )}
      <p className="text-[length:var(--text-xs)] text-ink-muted">
        Stored encrypted and never shown again. ShipLean only reads from Jira.
      </p>
      {saved !== null && !replacing ? (
        <p data-testid={ids.connectionSummary} className="text-[length:var(--text-sm)]">
          {saved.baseUrl}
          {saved.email === '' ? '' : ` · ${saved.email}`}
        </p>
      ) : null}
      <div className="flex items-center gap-[var(--space-2)]">
        {editing ? (
          <button
            type="button"
            data-testid={ids.save}
            className="btn-signal"
            disabled={!canSave || save.isPending}
            onClick={onSave}
          >
            Save
          </button>
        ) : null}
        <button
          type="button"
          data-testid={ids.test}
          className="btn-ghost"
          disabled={saved === null || replacing || test.isPending}
          onClick={onTest}
        >
          {test.isPending ? 'Testing…' : 'Test connection'}
        </button>
      </div>
      {save.error ? (
        <p data-testid={ids.saveError} role="alert" className="text-alert">
          {save.error instanceof ApiRequestError
            ? save.error.message
            : 'The connection was not saved.'}
        </p>
      ) : null}
      {test.error ? (
        <p role="alert" className="text-alert">
          The test could not be run.
        </p>
      ) : null}
      {result ? <ConnectionResult result={result} /> : null}
    </div>
  );
}
