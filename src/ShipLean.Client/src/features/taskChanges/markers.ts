import type { CodeCommentDto, CodeSide } from '@shiplean/core/api/taskChanges';

// One marker on a diff line: every thread anchored at the same place (commit, file, side, line) is one marker.
export interface LineMarker {
  key: string;
  repo: string;
  sha: string;
  path: string;
  side: CodeSide;
  line: number;
  /** Comments in the threads, replies included. */
  count: number;
  /** Threads not resolved. */
  open: number;
  /** True when any thread there is outdated. */
  outdated: boolean;
}

export const markerKey = (repo: string, sha: string, path: string, side: CodeSide, line: number) =>
  `${repo}\u0000${sha}\u0000${path}\u0000${side}\u0000${line}`;

export function deriveMarkers(comments: CodeCommentDto[]): LineMarker[] {
  const markers = new Map<string, LineMarker>();
  for (const c of comments) {
    if (c.parentId !== null || c.repo === null || c.sha === null || c.path === null) continue;
    if (c.side === null || c.line === null) continue;
    const key = markerKey(c.repo, c.sha, c.path, c.side, c.line);
    const marker = markers.get(key) ?? {
      key,
      repo: c.repo,
      sha: c.sha,
      path: c.path,
      side: c.side,
      line: c.line,
      count: 0,
      open: 0,
      outdated: false,
    };
    marker.count += 1 + c.replies.length;
    if (!c.resolved) marker.open += 1;
    if (c.outdated) marker.outdated = true;
    markers.set(key, marker);
  }
  return [...markers.values()];
}
