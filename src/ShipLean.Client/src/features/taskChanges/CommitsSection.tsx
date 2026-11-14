import type { TaskCommitsSectionDto } from '@shiplean/core/api/taskChanges';
import { taskChangesTestIds as ids } from './testIds';

export interface CommitsSectionProps {
  projectKey: string;
  itemKey: string;
  section: TaskCommitsSectionDto;
  /** "n of m files viewed" at the current head. */
  viewedCount: number;
  onAddCommit(repo: string | null, sha: string): Promise<void>;
}

// Stub: the section's rows, totals, add-commit box and Review changes link replace this.
export function CommitsSection({ section }: CommitsSectionProps) {
  return (
    <section data-testid={ids.section}>
      {section.commits.length} commits, {section.totals.files} files
    </section>
  );
}
