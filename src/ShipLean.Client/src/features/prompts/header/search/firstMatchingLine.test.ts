import { describe, expect, it } from 'vitest';
import { firstMatchingLine, lineExcerpt } from './firstMatchingLine';

const text =
  '{{common_brief}}\n\n  # Task {{task_key}}: {{task_title}}\n\nUse {{task_key}} again.\n';

describe('firstMatchingLine', () => {
  it('firstMatchingLine_WhenMatchInText_ShouldReturnTheLineAndRange', () => {
    expect(firstMatchingLine(text, 'TASK_KEY')).toEqual({
      line: '# Task {{task_key}}: {{task_title}}',
      start: 9,
      end: 17,
    });
  });

  it('firstMatchingLine_WhenQueryHasSpaces_ShouldMatchItTrimmed', () => {
    expect(firstMatchingLine(text, '  common_brief ')).toEqual({
      line: '{{common_brief}}',
      start: 2,
      end: 14,
    });
  });

  it('firstMatchingLine_WhenNoLineMatches_ShouldReturnNull', () => {
    expect(firstMatchingLine(text, 'sprint_name')).toBeNull();
  });

  it('firstMatchingLine_WhenQueryIsBlank_ShouldReturnNull', () => {
    expect(firstMatchingLine(text, '   ')).toBeNull();
  });
});

describe('lineExcerpt', () => {
  it('lineExcerpt_WhenMatchNearTheStart_ShouldKeepTheWholeLine', () => {
    expect(lineExcerpt({ line: '# Task {{task_key}}: x', start: 9, end: 17 })).toEqual({
      before: '# Task {{',
      match: 'task_key',
      after: '}}: x',
    });
  });

  it('lineExcerpt_WhenMatchFarIn_ShouldCutTheLeadToFortyCharacters', () => {
    const line = `${'a'.repeat(50)}needle tail`;
    expect(lineExcerpt({ line, start: 50, end: 56 })).toEqual({
      before: `…${'a'.repeat(40)}`,
      match: 'needle',
      after: ' tail',
    });
  });
});
