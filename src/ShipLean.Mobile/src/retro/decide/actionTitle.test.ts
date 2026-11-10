import { describe, expect, it } from 'vitest';
import { seedTitle, TITLE_SEED_LENGTH } from './actionTitle';

describe('action title seed', () => {
  it('cuts the seeded title at 80 characters without rewording', () => {
    const text =
      'Give each lane its own migration number range in the plan so two lanes never collide on one file.';
    expect(TITLE_SEED_LENGTH).toBe(80);
    expect(seedTitle(text)).toBe(text.slice(0, 80));
    expect(seedTitle(text)).toHaveLength(80);
    // Short text is copied as it is, spacing and case included.
    expect(seedTitle('  Profile the S22 FIRST ')).toBe('  Profile the S22 FIRST ');
    // A cut never leaves half of an emoji.
    const emoji = `${'a'.repeat(79)}😀tail`;
    expect(seedTitle(emoji)).toBe('a'.repeat(79));
  });
});
