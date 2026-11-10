import { describe, expect, it } from 'vitest';
import type { DecisionDto } from '@shiplean/core/api/asks';
import { discussQuote } from './discussQuote';

const decision = (question: string, currentAnswer: string) =>
  ({ label: 'D3', question, currentAnswer }) as DecisionDto;

describe('discussQuote', () => {
  it('discussQuote_WhenSingleLine_ShouldQuoteOneLineThenText', () => {
    expect(discussQuote(decision('What an expired card does', 'Hidden'), ' Why?\n')).toBe(
      '> **D3** · What an expired card does: Hidden\n\nWhy?',
    );
  });

  it('discussQuote_WhenMultilineQuestion_ShouldQuoteEveryLine', () => {
    expect(discussQuote(decision('First line\nsecond line\n\nfourth', 'Yes'), 'Text')).toBe(
      '> **D3** · First line\n> second line\n>\n> fourth: Yes\n\nText',
    );
  });
});
