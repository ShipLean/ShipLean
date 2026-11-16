import { parsePatchFiles } from '@pierre/diffs';
import type { FileDiffMetadata } from '@pierre/diffs';
import type { CodeSide } from '@shiplean/core/api/taskChanges';

/** The patch with the file header git's diff carries, which a bare hunk list lacks and the parser reads the file name from. */
export function withHeader(patch: string, path: string, oldPath: string | null): string {
  return `--- a/${oldPath ?? path}\n+++ b/${path}\n${patch}`;
}

export function parseFile(patch: string, path: string, oldPath: string | null) {
  return parsePatchFiles(withHeader(patch, path, oldPath))[0]?.files[0];
}

/** The text of a line, read from the parsed metadata (the hover only tells us the line and side). */
export function lineTextOf(
  meta: FileDiffMetadata | undefined,
  side: CodeSide,
  line: number,
): string {
  if (!meta) return '';
  for (const hunk of meta.hunks) {
    const start = side === 'new' ? hunk.additionStart : hunk.deletionStart;
    const count = side === 'new' ? hunk.additionCount : hunk.deletionCount;
    if (line < start || line >= start + count) continue;
    const index =
      (side === 'new' ? hunk.additionLineIndex : hunk.deletionLineIndex) + (line - start);
    const lines = side === 'new' ? meta.additionLines : meta.deletionLines;
    return (lines[index] ?? '').replace(/\r?\n$/, '');
  }
  return '';
}
