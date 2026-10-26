import type { ImportMappingDto, JiraProjectDto } from '../../../api/import';
import type { ProjectDto } from '../../../api/projects';
import { importTestIds as ids } from '../testIds';
import { JobView } from './JobView';
import { RunReady } from './RunReady';

// Step 5: the summary with Start (or Re-run), then the job it started. The job id lives in the URL (?job=), so a reload or
// a link from the jobs list lands on the same job.
export function RunStep({
  project,
  connectionId,
  jira,
  mapping,
  rerunMapping,
  jobId,
  onJob,
  onBack,
}: {
  project: ProjectDto;
  connectionId: string | null;
  jira: JiraProjectDto | null;
  // What the run would bring in; null while the suggestion has not been read.
  mapping: ImportMappingDto | null;
  // The mapping a re-run sends: null re-runs with the job's saved mapping (a step 4 that was skipped).
  rerunMapping: ImportMappingDto | null;
  jobId: string | null;
  onJob: (jobId: string) => void;
  onBack: () => void;
}) {
  return (
    <div data-testid={ids.runStep} className="flex flex-col gap-[var(--space-3)]">
      {jobId === null ? (
        <RunReady
          project={project}
          connectionId={connectionId}
          jira={jira}
          mapping={mapping}
          rerunMapping={rerunMapping}
          onJob={onJob}
          onBack={onBack}
        />
      ) : (
        <JobView project={project} jobId={jobId} onJob={onJob} />
      )}
    </div>
  );
}
