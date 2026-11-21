import { parseDiffFromFile } from '@pierre/diffs';
import { hunksOfMeta, type PromptDiffHunk } from '../detail/diffParse';

const FILE = { name: 'prompt.md' } as const;
const normalize = (text: string) => text.replaceAll('\r\n', '\n');

/** The editor's text against the built-in it starts from, in the line shape the prompt diff views read (`diffParse`). */
export function changesFromBuiltIn(builtIn: string, text: string): PromptDiffHunk[] {
  const meta = parseDiffFromFile(
    { ...FILE, contents: normalize(builtIn) },
    { ...FILE, contents: normalize(text) },
  );
  return hunksOfMeta(meta);
}

/** The added and removed line counts, for the tab's label. */
export function countChanges(hunks: PromptDiffHunk[]): { adds: number; dels: number } {
  const lines = hunks.flatMap((hunk) => hunk.lines);
  return {
    adds: lines.filter((line) => line.kind === 'add').length,
    dels: lines.filter((line) => line.kind === 'del').length,
  };
}
