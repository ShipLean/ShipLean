import { parsePatchFiles } from '@pierre/diffs';
import type { FileDiffMetadata } from '@pierre/diffs';
import { withHeader } from '../../taskChanges/diffLines';

/** The one name the header carries: the parser reads a file name from it, and a prompt has no path. */
const PROMPT_FILE = 'prompt.md';

export type PromptDiffLineKind = 'context' | 'add' | 'del';

/** One line of a prompt diff; each number is null on the side the line is not on. */
export interface PromptDiffLine {
  kind: PromptDiffLineKind;
  oldLine: number | null;
  newLine: number | null;
  text: string;
}

/** A hunk's lines, with the count of unchanged lines the diff skips before it (C11: the server's patch has no file header). */
export interface PromptDiffHunk {
  skippedBefore: number;
  lines: PromptDiffLine[];
}

/** The client's one parser of `PromptDiff.Patch` output (C11): a header-less patch, as `parsePatchFiles` reads it after a file header is added. */
export function parsePromptPatch(patch: string): FileDiffMetadata | undefined {
  return parsePatchFiles(withHeader(patch, PROMPT_FILE, null))[0]?.files[0];
}

const stripNewline = (line: string | undefined) => (line ?? '').replace(/\r?\n$/, '');

/** The patch as hunks of lines, ready for a view to render. An empty patch has no hunks. */
export function promptDiffHunks(patch: string): PromptDiffHunk[] {
  const meta = parsePromptPatch(patch);
  if (meta === undefined) return [];
  return meta.hunks.map((hunk) => {
    const lines: PromptDiffLine[] = [];
    let oldLine = hunk.deletionStart;
    let newLine = hunk.additionStart;
    for (const content of hunk.hunkContent) {
      if (content.type === 'context') {
        for (let i = 0; i < content.lines; i++) {
          lines.push({
            kind: 'context',
            oldLine: oldLine++,
            newLine: newLine++,
            text: stripNewline(meta.additionLines[content.additionLineIndex + i]),
          });
        }
        continue;
      }
      for (let i = 0; i < content.deletions; i++) {
        lines.push({
          kind: 'del',
          oldLine: oldLine++,
          newLine: null,
          text: stripNewline(meta.deletionLines[content.deletionLineIndex + i]),
        });
      }
      for (let i = 0; i < content.additions; i++) {
        lines.push({
          kind: 'add',
          oldLine: null,
          newLine: newLine++,
          text: stripNewline(meta.additionLines[content.additionLineIndex + i]),
        });
      }
    }
    return { skippedBefore: hunk.collapsedBefore, lines };
  });
}
