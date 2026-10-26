/** The shortest password the server accepts (Passwords.MinLength). */
export const MIN_PASSWORD_LENGTH = 10;

export interface PasswordCheck {
  label: string;
  met: boolean;
}

/** The live checks under the password form. The server decides; these tell you what it will ask for. */
export function passwordChecks(current: string, next: string, confirm: string): PasswordCheck[] {
  return [
    { label: 'Current password entered', met: current.length > 0 },
    {
      label: `At least ${MIN_PASSWORD_LENGTH} characters`,
      met: next.length >= MIN_PASSWORD_LENGTH,
    },
    { label: 'Different from the current one', met: next.length > 0 && next !== current },
    { label: 'Both match', met: next.length > 0 && next === confirm },
  ];
}

export const passwordReady = (checks: PasswordCheck[]) => checks.every((c) => c.met);
