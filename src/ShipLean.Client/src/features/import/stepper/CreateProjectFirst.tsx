import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { ApiRequestError } from '../../../api/client';
import { routePath } from '../../../routes';
import { createProject, useKeyCheck } from '../importApi';
import { importTestIds as ids } from '../testIds';
import { EarlierImports } from './EarlierImports';
import { KeyCheckResult } from './KeyCheckResult';

const WORKING_DAYS = [1, 2, 3, 4, 5];

// Import with no project yet: the Jira connection belongs to a project, so ShipLean creates the empty project first
// (no Sprint 1: the import brings Jira's sprints) and carries on at that project's import.
export function CreateProjectFirst() {
  const navigate = useNavigate();
  const [name, setName] = useState('');
  const [key, setKey] = useState('');
  const check = useKeyCheck(key);
  const create = useMutation({
    mutationFn: () =>
      createProject({
        name: name.trim(),
        key,
        copyFromProjectId: null,
        memberEmails: [],
        workingDays: WORKING_DAYS,
        firstSprint: null,
      }),
    onSuccess: (project) => navigate(routePath('jiraImport', { key: project.key })),
  });
  const valid = name.trim() !== '' && key !== '' && check.data?.available === true;
  return (
    <section
      data-testid={ids.createFirst}
      className="panel flex max-w-xl flex-col gap-[var(--space-3)] p-[var(--space-4)]"
    >
      <EarlierImports />
      <h2 className="font-semibold">Import from Jira into a new project</h2>
      <p className="text-ink-muted">
        We&apos;ll create {key === '' ? 'the project' : key} first so its Jira connection has a
        home.
      </p>
      <label className="flex flex-col gap-1 text-[length:var(--text-sm)]">
        Project name
        <input
          data-testid={ids.createName}
          className="field"
          value={name}
          onChange={(event) => setName(event.target.value)}
        />
      </label>
      <label className="flex flex-col gap-1 text-[length:var(--text-sm)]">
        Project key
        <input
          data-testid={ids.createKey}
          className="field font-mono"
          value={key}
          maxLength={10}
          spellCheck={false}
          autoComplete="off"
          onChange={(event) => setKey(event.target.value.toUpperCase())}
        />
      </label>
      {key === '' ? null : <KeyCheckResult check={check.data} />}
      <p className="text-[length:var(--text-xs)] text-ink-muted">
        The key can&apos;t change after the project is created.
      </p>
      {create.error ? (
        <p data-testid={ids.createError} role="alert" className="text-alert">
          {create.error instanceof ApiRequestError
            ? create.error.message
            : 'The project was not created.'}
        </p>
      ) : null}
      <div>
        <button
          type="button"
          data-testid={ids.createNext}
          className="btn-signal"
          disabled={!valid || create.isPending}
          onClick={() => create.mutate()}
        >
          Next: Connection
        </button>
      </div>
    </section>
  );
}
