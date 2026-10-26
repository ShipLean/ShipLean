import type { JiraTestResultDto } from '../../../api/import';
import { ConnectionPanel } from '../connection/ConnectionPanel';

export function ConnectionStep({
  projectId,
  onTested,
}: {
  projectId: string;
  onTested: (result: JiraTestResultDto | null) => void;
}) {
  return (
    <section className="panel flex flex-col gap-[var(--space-3)] p-[var(--space-4)]">
      <h2 className="font-semibold">Jira site</h2>
      <ConnectionPanel projectId={projectId} onTested={onTested} />
    </section>
  );
}
