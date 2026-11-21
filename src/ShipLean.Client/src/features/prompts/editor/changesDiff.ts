import { parseDiffFromFile } from '@pierre/diffs';
import type { PromptDiffHunk, PromptDiffLine } from '../detail/diffParse';

const FILE = { name: 'prompt.md' } as const;
const normalize = (text: string) => text.replaceAll('\r\n', '\n');
const stripNewline = (line: string | undefined) => (line ?? '').replace(/\r?\n$/, '');

/** The editor's text against the built-in it starts from, in the line shape the prompt diff views read (`diffParse`). */
export function changesFromBuiltIn(builtIn: string, text: string): PromptDiffHunk[] {
  const meta = parseDiffFromFile(
    { ...FILE, contents: normalize(builtIn) },
    { ...FILE, contents: normalize(text) },
  );
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

/** The added and removed line counts, for the tab's label. */
export function countChanges(hunks: PromptDiffHunk[]): { adds: number; dels: number } {
  const lines = hunks.flatMap((hunk) => hunk.lines);
  return {
    adds: lines.filter((line) => line.kind === 'add').length,
    dels: lines.filter((line) => line.kind === 'del').length,
  };
}
