import { describe, expect, it } from 'vitest';
import { aboutOf, keyNamedIn } from './about';

describe('retro point about', () => {
  it('finds the first key of this project in the text', () => {
    const text = 'Profile the small model before SHIP-906 and again after SHIP-12, not OTHER-5.';
    expect(keyNamedIn(text, 'SHIP')).toBe('SHIP-906');
    expect(aboutOf(text, 'SHIP', 'sprint-25')).toEqual({ key: 'SHIP-906', sprintId: null });
  });

  it('is about the sprint when no key of this project is named', () => {
    expect(aboutOf('Slow builds, see OTHER-5 and NOTSHIP-9.', 'SHIP', 'sprint-25')).toEqual({
      key: null,
      sprintId: 'sprint-25',
    });
    expect(aboutOf('No key at all.', 'SHIP', 'sprint-25')).toEqual({
      key: null,
      sprintId: 'sprint-25',
    });
    expect(aboutOf('SHIP-1', undefined, 'sprint-25')).toEqual({ key: null, sprintId: 'sprint-25' });
  });
});
