import type { TaskChangeFilePathDto } from '@shiplean/core/api/taskChanges';

// Which files the reviewer marked Viewed, kept in this browser only (D12). An entry holds the patch id the file had when it
// was marked and the head commit it was last confirmed at, so a new commit clears Viewed only on files whose patch changed (D22).
export interface ViewedEntry {
  patchId: string;
  headCommitId: string | null;
}
export type ViewedState = Record<string, ViewedEntry>;

const keyOf = (workItemId: string) => `shiplean.taskChanges.viewed.${workItemId}`;
export const fileKey = (repo: string, path: string) => `${repo}\u0000${path}`;

const isEntry = (value: unknown): value is ViewedEntry => {
  if (typeof value !== 'object' || value === null) return false;
  const v = value as Record<string, unknown>;
  return (
    typeof v.patchId === 'string' && (v.headCommitId === null || typeof v.headCommitId === 'string')
  );
};

// Storage that throws or holds garbage reads as nothing viewed.
export function readViewed(workItemId: string): ViewedState {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(keyOf(workItemId)) ?? '{}');
    if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) return {};
    return Object.fromEntries(Object.entries(parsed).filter(([, entry]) => isEntry(entry)));
  } catch {
    return {};
  }
}

export function saveViewed(workItemId: string, state: ViewedState): void {
  try {
    localStorage.setItem(keyOf(workItemId), JSON.stringify(state));
  } catch {
    // Not kept: the marks last until the page closes.
  }
}

/** A file is viewed while the patch it was marked with is the patch it has now. */
export const isViewed = (
  state: ViewedState,
  repo: string,
  path: string,
  patchId: string,
): boolean => state[fileKey(repo, path)]?.patchId === patchId;

export function setViewed(
  workItemId: string,
  repo: string,
  path: string,
  patchId: string,
  headCommitId: string | null,
  viewed: boolean,
): ViewedState {
  const next = { ...readViewed(workItemId) };
  if (viewed) next[fileKey(repo, path)] = { patchId, headCommitId };
  else delete next[fileKey(repo, path)];
  saveViewed(workItemId, next);
  return next;
}

/**
 * The marks after loading the page at `headCommitId` (save them with saveViewed). Pure: it is given the patch ids of the page loaded at `headCommitId`: marks whose patch still matches move to the new head,
 * the rest are dropped. Compared by headCommitId, never by sha, because one sha can sit in two repositories.
 */
export function restampViewed(
  stored: ViewedState,
  headCommitId: string | null,
  current: { repo: string; path: string; patchId: string }[],
): ViewedState {
  const next: ViewedState = {};
  for (const file of current) {
    const entry = stored[fileKey(file.repo, file.path)];
    if (entry?.patchId === file.patchId)
      next[fileKey(file.repo, file.path)] = { ...entry, headCommitId };
  }
  return next;
}

/** "n of m files viewed" for the Task page: the marks confirmed at the current head. */
export function viewedCount(
  state: ViewedState,
  headCommitId: string | null,
  paths: TaskChangeFilePathDto[],
): number {
  return paths.filter((p) => {
    const entry = state[fileKey(p.repo, p.path)];
    return entry !== undefined && entry.headCommitId === headCommitId;
  }).length;
}
