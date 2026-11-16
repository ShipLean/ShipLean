import type { TaskChangesDto, TaskCommitsSectionDto } from '@shiplean/core/api/taskChanges';
import noAccess from '../../../../../ShipLean.TestSupport/DtoFixtures/taskChanges/changes-noaccess.json' with { type: 'json' };
import perCommit from '../../../../../ShipLean.TestSupport/DtoFixtures/taskChanges/changes-per-commit.json' with { type: 'json' };
import { CHANGES, SECTION } from '../../../features/taskChanges/ct/fixtures';

export { CHANGES, SECTION };

// A commit that no instance token can read: the page keeps its row and shows "open on the host" instead of a diff.
export const CHANGES_NO_ACCESS = noAccess.changesNoAccess as TaskChangesDto;

// One commit selected, as the commit picker's "one commit" rows show it.
export const CHANGES_PER_COMMIT = perCommit.changesPerCommit as TaskChangesDto;

// The reviewer's thread whose line a later commit changed: it keeps the old line text and is marked outdated.
export const CHANGES_OUTDATED: TaskChangesDto = {
  ...CHANGES,
  comments: CHANGES.comments.map((comment) => ({ ...comment, outdated: true })),
};

// A Task that has no commits yet.
export const SECTION_EMPTY: TaskCommitsSectionDto = {
  ...SECTION,
  commits: [],
  totals: { ...SECTION.totals, commits: 0, files: 0, additions: 0, deletions: 0 },
  filePaths: [],
  openComments: 0,
  headCommitId: null,
};
