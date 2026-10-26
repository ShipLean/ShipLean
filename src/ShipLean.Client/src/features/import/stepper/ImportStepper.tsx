import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import type { ProjectDto } from '../../../api/projects';
import { projectByKeyKey } from '../../../queries/shared';
import { registerInvalidation } from '../../../realtime';
import type { ImportMappingDto } from '../../../api/import';
import { ApiRequestError } from '../../../api/client';
import { useJiraConnection, useJiraProjects, useSuggestedMapping } from '../importApi';
import { MappingStep } from '../mapping/MappingStep';
import { RunStep } from '../run/RunStep';
import { importTestIds as ids } from '../testIds';
import { ConnectionStep } from './ConnectionStep';
import { JiraProjectStep } from './JiraProjectStep';
import { StepFooter } from './StepFooter';
import { StepNav } from './StepNav';
import { TargetStep } from './TargetStep';
import { backStep, describeSteps, nextLabel, nextStep, targetOf } from './stepModel';

// Steps 1 to 4 of the import into this project. Whether the connection works is asked of Jira in this visit; the
// Jira project and the target are derived from what was picked.
export function ImportStepper({ project }: { project: ProjectDto }) {
  const queryClient = useQueryClient();
  // The job on step 5 is in the URL (?job=), so a link or a reload opens that job.
  const [params, setParams] = useSearchParams();
  const jobId = params.get('job');
  // A re-run that needs the mapping looked at again opens on Mapping for that job's Jira project (?rerun=&jira=).
  const rerunId = params.get('rerun');
  const rerunJira = rerunId === null ? null : params.get('jira');
  const [step, setStep] = useState(jobId !== null ? 5 : rerunId !== null ? 4 : 1);
  // A job opened from elsewhere (the jobs list) takes the stepper to its run.
  const [seenJob, setSeenJob] = useState(jobId);
  if (jobId !== seenJob) {
    setSeenJob(jobId);
    if (jobId !== null) setStep(5);
  }
  const [tested, setTested] = useState(rerunId !== null);
  const [jiraKey, setJiraKey] = useState<string | null>(rerunJira);
  const [seenRerun, setSeenRerun] = useState(rerunId);
  if (rerunId !== seenRerun) {
    setSeenRerun(rerunId);
    if (rerunId !== null) {
      setTested(true);
      setJiraKey(rerunJira);
      setStep(4);
    }
  }
  // The mapping as edited, for the Jira project and previous job it was read for; it is the step's output.
  const [edited, setEdited] = useState<{ for: string; value: ImportMappingDto } | null>(null);
  const connection = useJiraConnection(project.id);
  const projects = useJiraProjects(project.id, tested);
  const jira = projects.data?.find((p) => p.key === jiraKey) ?? null;

  // The project's emptiness comes from the shared project read; a work item made elsewhere changes it.
  useEffect(
    () =>
      registerInvalidation('workItemCreated', () => {
        void queryClient.invalidateQueries({ queryKey: projectByKeyKey(project.key) });
      }),
    [queryClient, project.key],
  );

  const target = jira === null ? null : targetOf(project, jira);
  const ready = target?.kind === 'ready';
  // The suggestion is read once the step can be reached; a re-run reads it early to learn whether step 4 is skipped.
  const suggested = useSuggestedMapping(
    project.id,
    connection.data?.id ?? null,
    jira?.key ?? null,
    jira?.importedAs?.jobId ?? null,
    ready && (target.rerun || step >= 4),
  );
  const suggestionKey = `${jira?.key ?? ''}|${jira?.importedAs?.jobId ?? ''}`;
  const mapping = edited?.for === suggestionKey ? edited.value : (suggested.data ?? null);
  const { canNext, reachable, summaries, hints, skipped } = describeSteps({
    edited: edited?.for === suggestionKey,
    baseUrl: connection.data?.baseUrl ?? null,
    tested,
    jira,
    target,
    projectKey: project.key,
    mapping,
  });
  const mappingReadError = suggested.isError ? (
    <p data-testid={ids.mappingError} role="alert" className="text-alert">
      {suggested.error instanceof ApiRequestError
        ? suggested.error.message
        : 'The mapping could not be read from Jira.'}
    </p>
  ) : null;
  const awaitingSkipAnswer = step === 3 && ready && target.rerun && mapping === null;
  const goNext = () => setStep(nextStep(step, skipped));
  const openJob = (id: string) => setParams({ job: id }, { replace: true });

  if (project.role !== 'admin') {
    return (
      <p data-testid={ids.notAdmin} className="text-ink-muted">
        Only a project admin can import from Jira.
      </p>
    );
  }

  return (
    <div
      data-testid={ids.stepper}
      className="flex flex-col gap-[var(--space-4)] p-[var(--space-4)]"
    >
      <header>
        <h1 className="page-title">Import from Jira</h1>
        <p className="text-ink-muted">
          One Jira project into one ShipLean project. Nothing is written back to Jira.
        </p>
      </header>
      <StepNav step={step} reachable={reachable} summaries={summaries} onGo={setStep} />
      {step === 1 ? (
        <ConnectionStep
          projectId={project.id}
          onTested={(result) => setTested(result?.ok === true)}
        />
      ) : null}
      {step === 2 ? (
        <JiraProjectStep projectId={project.id} selected={jiraKey} onPick={setJiraKey} />
      ) : null}
      {step === 3 && jira !== null ? <TargetStep project={project} jira={jira} /> : null}
      {step === 3 && awaitingSkipAnswer ? mappingReadError : null}
      {step === 4 && mapping !== null ? (
        <MappingStep
          projectId={project.id}
          mapping={mapping}
          onChange={(value) => setEdited({ for: suggestionKey, value })}
        />
      ) : null}
      {step === 4 && mapping === null
        ? (mappingReadError ?? (
            <p data-testid={ids.mappingLoading} className="text-ink-muted">
              Reading the Jira project…
            </p>
          ))
        : null}
      {step === 5 ? (
        <RunStep
          project={project}
          connectionId={connection.data?.id ?? null}
          jira={jira}
          mapping={mapping}
          rerunMapping={skipped ? null : mapping}
          jobId={jobId}
          onJob={openJob}
          onBack={() => setStep(backStep(step, skipped))}
        />
      ) : null}
      <StepFooter
        step={step}
        hint={hints[step - 1]}
        canNext={canNext[step - 1] && !awaitingSkipAnswer}
        nextLabel={nextLabel(step, skipped)}
        onBack={() => setStep(backStep(step, skipped))}
        onNext={goNext}
      />
    </div>
  );
}
