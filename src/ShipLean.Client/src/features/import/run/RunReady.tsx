import { ApiRequestError } from '../../../api/client';
import type { ImportMappingDto, JiraProjectDto } from '../../../api/import';
import type { ProjectDto } from '../../../api/projects';
import { importTestIds as ids } from '../testIds';
import { newStateCount, newUserCount } from '../stepper/stepModel';
import { useRerunImport, useStartImport } from './runApi';

const n = (value: number) => value.toLocaleString('en-GB');

// "Ready to import": what will come in and the button that queues the job. A Jira project imported here before re-runs
// that job; anything else starts a new one into this (empty) project.
export function RunReady({
  project,
  connectionId,
  jira,
  mapping,
  rerunMapping,
  onJob,
  onBack,
}: {
  project: ProjectDto;
  connectionId: string | null;
  jira: JiraProjectDto | null;
  mapping: ImportMappingDto | null;
  rerunMapping: ImportMappingDto | null;
  onJob: (jobId: string) => void;
  onBack: () => void;
}) {
  const start = useStartImport(project.id);
  const rerun = useRerunImport(project.id);
  const previousJobId = jira?.importedAs?.jobId ?? null;
  const pending = start.isPending || rerun.isPending;
  const failure = start.error ?? rerun.error;
  const begin = () => {
    if (jira === null) return;
    if (previousJobId !== null) {
      rerun.mutate(
        { jobId: previousJobId, mapping: rerunMapping },
        { onSuccess: (j) => onJob(j.id) },
      );
    } else if (connectionId !== null && mapping !== null) {
      start.mutate(
        {
          connectionId,
          jiraProjectKey: jira.key,
          target: { kind: 'existingProject', key: null, name: null, projectId: project.id },
          mapping,
        },
        { onSuccess: (j) => onJob(j.id) },
      );
    }
  };
  const totals = mapping?.totals ?? null;
  return (
    <section
      data-testid={ids.runReady}
      className="panel flex flex-col gap-[var(--space-3)] p-[var(--space-4)]"
    >
      <h2 className="font-semibold">
        {previousJobId === null ? 'Ready to import' : 'Ready to re-run'}
      </h2>
      <p className="font-mono">
        {jira?.key ?? ''} → {project.key}
      </p>
      {totals === null ? null : (
        <p data-testid={ids.runTotals} className="text-[length:var(--text-sm)]">
          {n(totals.issues)} issues · {n(totals.comments)} comments · {n(totals.attachments)}{' '}
          attachments · {n(totals.revisions)} revisions · {n(totals.sprints)} sprints ·{' '}
          {n(totals.people)} people
        </p>
      )}
      {mapping === null ? null : (
        <p data-testid={ids.runReadyNew} className="text-[length:var(--text-sm)]">
          {n(newUserCount(mapping))} new users · {n(newStateCount(mapping))} new states
        </p>
      )}
      <p className="text-[length:var(--text-sm)] text-ink-muted">
        You can leave the page; the job keeps running and anyone on this screen sees its progress.
      </p>
      {failure ? (
        <p data-testid={ids.runStartError} role="alert" className="text-alert">
          {failure instanceof ApiRequestError ? failure.message : 'The import did not start.'}
        </p>
      ) : null}
      <div className="flex gap-[var(--space-2)]">
        <button
          type="button"
          data-testid={previousJobId === null ? ids.runStart : ids.runRerun}
          className="btn-signal"
          disabled={pending || mapping === null || jira === null}
          onClick={begin}
        >
          {previousJobId === null ? 'Start import' : 'Re-run'}
        </button>
        <button
          type="button"
          data-testid={ids.runBackToMapping}
          className="btn-ghost"
          onClick={onBack}
        >
          Back
        </button>
      </div>
    </section>
  );
}
