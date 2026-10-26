import { describe, expect, it } from 'vitest';
import { slugFromTitle } from './slugFromTitle';

describe('slugFromTitle', () => {
  it('slugFromTitle_WhenWordsAndPunctuation_ShouldJoinWithSingleHyphens', () => {
    expect(slugFromTitle('  Express checkout! (v2) ')).toBe('express-checkout-v2');
  });

  it('slugFromTitle_WhenAccented_ShouldDropTheAccent', () => {
    expect(slugFromTitle('Café Crème')).toBe('cafe-creme');
  });

  it('slugFromTitle_WhenNothingToKeep_ShouldBeEmpty', () => {
    expect(slugFromTitle('???')).toBe('');
  });

  it('slugFromTitle_WhenTooLong_ShouldCutAt64WithoutTrailingHyphen', () => {
    const slug = slugFromTitle(`${'a'.repeat(63)} bbb`);
    expect(slug).toBe('a'.repeat(63));
  });
});
