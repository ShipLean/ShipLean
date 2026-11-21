import { describe, expect, it } from 'vitest';
import { hasAgentTagLines, highlightLine, highlightSource } from './highlight';

describe('highlightLine', () => {
  it('HighlightLine_WhenHeading_ShouldBeOneHeadingSpan', () => {
    expect(highlightLine('## Steps')).toEqual([{ kind: 'heading', text: '## Steps' }]);
  });

  it('HighlightLine_WhenHashWithoutSpace_ShouldNotBeAHeading', () => {
    expect(highlightLine('#hashtag')).toEqual([{ kind: 'plain', text: '#hashtag' }]);
  });

  it('HighlightLine_WhenPlaceholders_ShouldSplitThemOut', () => {
    expect(highlightLine('Task {{key}} for {{project}}.')).toEqual([
      { kind: 'plain', text: 'Task ' },
      { kind: 'placeholder', text: '{{key}}' },
      { kind: 'plain', text: ' for ' },
      { kind: 'placeholder', text: '{{project}}' },
      { kind: 'plain', text: '.' },
    ]);
  });

  it('HighlightLine_WhenTagOnItsOwnLine_ShouldBeAnAgentTag', () => {
    expect(highlightLine('<claude-code>')).toEqual([{ kind: 'agentTag', text: '<claude-code>' }]);
    expect(highlightLine('</cursor>')).toEqual([{ kind: 'agentTag', text: '</cursor>' }]);
  });

  it('HighlightLine_WhenTagMidSentence_ShouldNotBeHighlighted', () => {
    expect(highlightLine('Use <claude-code> only here')).toEqual([
      { kind: 'plain', text: 'Use <claude-code> only here' },
    ]);
  });

  it('HighlightLine_WhenUnknownTag_ShouldStayPlain', () => {
    expect(highlightLine('<other>')).toEqual([{ kind: 'plain', text: '<other>' }]);
  });
});

describe('highlightSource', () => {
  it('HighlightSource_WhenTrailingNewline_ShouldNotAddAnEmptyLine', () => {
    expect(highlightSource('a\nb\n').map((l) => l.number)).toEqual([1, 2]);
  });

  it('HasAgentTagLines_WhenOnlyMidSentence_ShouldBeFalse', () => {
    expect(hasAgentTagLines('x <cursor> y')).toBe(false);
    expect(hasAgentTagLines('x\n<cursor>\ny\n</cursor>')).toBe(true);
  });
});
