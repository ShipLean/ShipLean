import type { JiraTestResultDto } from '../../../api/import';
import { useJiraConnection } from '../importApi';
import { ConnectionForm } from './ConnectionForm';

// The Jira connection of one project: base URL, Cloud (email and API token) or Server (personal access token). The
// token is write-only; a saved one shows its last four characters and Replace. Used by the import stepper and the
// project settings Integrations tab.
export function ConnectionPanel({
  projectId,
  onTested,
}: {
  projectId: string;
  // Called with each Test connection answer, and with null when the saved connection changes.
  onTested?: (result: JiraTestResultDto | null) => void;
}) {
  const connection = useJiraConnection(projectId);
  if (connection.isPending) {
    return <p className="text-ink-muted">Loading the connection…</p>;
  }
  if (connection.isError) {
    return (
      <p role="alert" className="text-alert">
        The connection could not be loaded.
      </p>
    );
  }
  return <ConnectionForm projectId={projectId} saved={connection.data} onTested={onTested} />;
}
