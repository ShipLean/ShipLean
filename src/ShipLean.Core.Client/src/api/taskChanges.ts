// A Task or Bug's commits, their diff and the code comments on it. C#: ShipLean.Contracts.TaskChanges.Dtos. Nothing is
// omitted: absent values are null; times are labels formatted by the server.
export const COMMIT_PROVIDERS = ['github', 'gitlab'] as const;
export type CommitProvider = (typeof COMMIT_PROVIDERS)[number];

/** `unreadable` is a repository the instance token cannot read; `missing` is a sha the repository does not have. */
export const COMMIT_ACCESS = ['ok', 'unreadable', 'missing'] as const;
export type CommitAccess = (typeof COMMIT_ACCESS)[number];

export const DIFF_FILE_STATUSES = ['added', 'modified', 'deleted', 'renamed'] as const;
export type DiffFileStatus = (typeof DIFF_FILE_STATUSES)[number];

export const CODE_SIDES = ['old', 'new'] as const;
export type CodeSide = (typeof CODE_SIDES)[number];

/** `combined`: the commits follow one another and show as one diff; `perCommit`: one diff each; `single`: one commit. */
export const CHANGES_MODES = ['combined', 'perCommit', 'single'] as const;
export type ChangesMode = (typeof CHANGES_MODES)[number];

/** A commit is always named by its work_item_commit id, never a bare sha: one sha can sit in two repositories. */
export interface TaskCommitRowDto {
  id: string;
  repo: string;
  repoUrl: string;
  provider: CommitProvider;
  sha: string;
  shortSha: string;
  subject: string;
  authorName: string;
  authoredAtLabel: string;
  addedBy: string;
  addedByAgent: boolean;
  additions: number;
  deletions: number;
  filesChanged: number;
  commitUrl: string;
  access: CommitAccess;
}

export interface TaskCommitTotalsDto {
  commits: number;
  /** Distinct (repo, path) pairs across the commits. */
  files: number;
  additions: number;
  deletions: number;
}

export interface TaskChangeFilePathDto {
  repo: string;
  path: string;
}

export interface TaskCommitsSectionDto {
  workItemId: string;
  commits: TaskCommitRowDto[];
  totals: TaskCommitTotalsDto;
  filePaths: TaskChangeFilePathDto[];
  openComments: number;
  /** The id of the newest live commit; null when there are none. */
  headCommitId: string | null;
}

export interface DiffFileDto {
  path: string;
  oldPath: string | null;
  status: DiffFileStatus;
  additions: number;
  deletions: number;
  /** The git unified diff of the file. */
  patch: string;
  /** Hex sha-1 of `patch`: a file whose diff did not change keeps its Viewed mark. */
  patchId: string;
  openComments: number;
}

export interface DiffPartDto {
  repo: string;
  /** Set for a single commit. */
  sha: string | null;
  /** Set with `headSha` for a combined range. */
  baseSha: string | null;
  headSha: string;
  files: DiffFileDto[];
}

export interface CodeCommentDto {
  id: string;
  parentId: string | null;
  repo: string | null;
  sha: string | null;
  path: string | null;
  side: CodeSide | null;
  line: number | null;
  startLine: number | null;
  lineText: string | null;
  bodyHtml: string;
  by: string;
  byAgent: boolean;
  createdAtLabel: string;
  resolved: boolean;
  outdated: boolean;
  sentBack: boolean;
  replies: CodeCommentDto[];
}

export interface TaskChangesDto {
  workItemId: string;
  itemKey: string;
  itemTitle: string;
  itemType: 'task' | 'bug';
  stateCategory: string;
  commits: TaskCommitRowDto[];
  /** "all" or a work_item_commit id. */
  selected: string;
  mode: ChangesMode;
  headCommitId: string | null;
  parts: DiffPartDto[];
  comments: CodeCommentDto[];
  openComments: number;
}

export interface ReviewOutcomeDto {
  state: string;
  openComments: number;
}

export interface AddTaskCommitRequest {
  repo: string | null;
  sha: string;
}
export interface AddCodeCommentRequest {
  repo: string;
  sha: string;
  path: string;
  side: CodeSide;
  line: number;
  startLine: number | null;
  lineText: string;
  body: string;
}
export interface ReplyCodeCommentRequest {
  body: string;
}
export interface ResolveCodeCommentRequest {
  resolved: boolean;
}
export interface ApproveChangesRequest {
  note: string | null;
  headCommitId: string;
}
export interface RequestTaskChangesRequest {
  note: string | null;
  keepAssignee: boolean;
}
