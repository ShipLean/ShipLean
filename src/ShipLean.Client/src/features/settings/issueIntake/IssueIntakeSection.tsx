import { useQuery } from '@tanstack/react-query';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { Panel } from '../draft/Panel';
import { settingsTestIds as ids } from '../testIds';
import { fetchIssueIntake, issueIntakeKey } from './api';
import { IssueIntakeForm } from './IssueIntakeForm';

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
