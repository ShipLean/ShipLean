import { describe, expect, it } from 'vitest';
import fixture from '../../../../../ShipLean.TestSupport/DtoFixtures/prompts/prompt-sections.json';
import { checkSections, countSections, filterSections, fixTagLine } from './agentSections';

interface Case {
  name: string;
  text: string;
  filtered: Record<string, string>;
  problems: { line: number; kind: string; tag: string; suggestion: string | null }[];
}
const cases = (fixture as { cases: Case[] }).cases;

describe('agentSections fixture cases', () => {
  for (const c of cases) {
    it(`checkSections_${c.name}_ShouldReportThePinnedProblems`, () => {
      expect(checkSections(c.text)).toEqual(c.problems);
    });
    for (const [agent, expected] of Object.entries(c.filtered)) {
      it(`filterSections_${c.name}_For_${agent}_ShouldEqualThePinnedText`, () => {
        expect(filterSections(c.text, agent)).toBe(expected);
      });
    }
  }
});

describe('agentSections helpers', () => {
  it('countSections_WhenTwoSectionsOfOneAgent_ShouldCountTwo', () => {
    const text = '<cursor>\na\n</cursor>\n<cursor>\nb\n</cursor>\n<claude-code>\nc\n</claude-code>';
    expect(countSections(text)).toEqual({ 'claude-code': 1, 'codex-cli': 0, cursor: 2 });
  });

  it('fixTagLine_WhenOpenAndCloseTags_ShouldReplaceOnlyThatLine', () => {
    const text = '<claudecode>\nx\n</claudecode>';
    expect(fixTagLine(fixTagLine(text, 1, 'claude-code'), 3, 'claude-code')).toBe(
      '<claude-code>\nx\n</claude-code>',
    );
  });
});
