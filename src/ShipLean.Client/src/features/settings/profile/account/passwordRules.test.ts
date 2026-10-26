import { describe, expect, it } from 'vitest';
import { passwordChecks, passwordReady } from './passwordRules';

describe('passwordChecks', () => {
  it('is ready only when all four checks hold', () => {
    expect(passwordReady(passwordChecks('old-password', 'a-new-password', 'a-new-password'))).toBe(
      true,
    );
  });

  it.each([
    ['no current', '', 'a-new-password', 'a-new-password'],
    ['too short', 'old-password', 'short', 'short'],
    ['same as current', 'old-password-1', 'old-password-1', 'old-password-1'],
    ['mismatch', 'old-password', 'a-new-password', 'a-new-passwore'],
  ])('is not ready: %s', (_name, current, next, confirm) => {
    expect(passwordReady(passwordChecks(current, next, confirm))).toBe(false);
  });
});
